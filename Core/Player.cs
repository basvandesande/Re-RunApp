namespace Re_RunApp.Core;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

internal class Player
{
    private readonly GpxProcessor _gpx;
    private readonly ITreadmill _treadmill;
    private readonly IHeartRate _heartRate;
    private readonly RunSettings? _runSettings;
    private bool _isPlaying = false;

    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<PlayerStatistics> OnTrackReady;
    public event Action<decimal,decimal> OnTrackChange; 
    public event Action<PlayerStatistics> OnStatisticsUpdate;
    private PlayerStatistics _playerStatistics = new();

    private decimal? _totalDistanceM = 0;
    private DateTime _startTime;
    private DateTime _lastStatisticsUpdate = DateTime.MinValue;
    
    private static readonly TimeSpan StatisticsUpdateInterval = TimeSpan.FromSeconds(4);
    private bool _firstStatisticsUpdate = true;



    public Player(GpxProcessor gpx, ITreadmill treadmill, IHeartRate heartRate, RunSettings runSettings)
    {
        _gpx = gpx ?? throw new Exception("GpxProcessor is null");
        _runSettings = runSettings;

        _treadmill = treadmill ?? throw new Exception("Treadmill is null");
        _treadmill.OnStatisticsUpdate += Treadmill_OnStatisticsUpdate;
        _treadmill.OnStatusUpdate += Treadmill_OnStatusUpdate;
     
        _heartRate = heartRate;
        _heartRate.OnHeartPulse += HeartRate_OnHeartPulse;
    }


    private void HeartRate_OnHeartPulse(int heartRate)
    {
        if (_heartRate.Enabled == false) return;
        _playerStatistics.CurrentHeartRate = heartRate;
    }

    private void Treadmill_OnStatisticsUpdate(TreadmillStatistics e)
    {
        var now = DateTime.UtcNow;
        if (_firstStatisticsUpdate || (now - _lastStatisticsUpdate) < StatisticsUpdateInterval)
        {
            _firstStatisticsUpdate = false;
            return; // Ignore events that come in too quickly, except the start :)
        }

        _lastStatisticsUpdate = now;

        if (_isPlaying)
        {
            _totalDistanceM = (decimal)e.DistanceM;

            _playerStatistics.CurrentDistanceM = _totalDistanceM < _playerStatistics.CurrentDistanceM ?
                                                _playerStatistics.CurrentDistanceM + _totalDistanceM :
                                                _totalDistanceM;

            _playerStatistics.CurrentSpeedKMH = e.SpeedKMH;

           
            // Forward the throttled update
            OnStatisticsUpdate?.Invoke(_playerStatistics);
        }
    }

    private void Treadmill_OnStatusUpdate(string e)
    {
        if (e == "STOP" || e == "STOP KEY")
        {
            Console.WriteLine("Treadmill stopped.");
            Task.Run(async () => await StopAsync());
        }
        else
        {
            Debug.WriteLine(e);
        }
    }

    public async Task StartAsync()
    {
        if (_gpx?.Tracks?.Length == 0) throw new Exception("No tracks to play");

        if (!_isPlaying) await _treadmill.StartAsync();

        _isPlaying = true;
        _startTime = DateTime.UtcNow;

        await _treadmill.ResetAsync();

        _cancellationTokenSource = new CancellationTokenSource();
        _ = BackgroundLoop(_cancellationTokenSource.Token);
    }

    public async Task StopAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _isPlaying = false;
            await _treadmill.StopAsync();
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("Player stop requested");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public decimal? GetDistanceRan() => _playerStatistics.CurrentDistanceM;

    private async Task BackgroundLoop(CancellationToken token)
    {
        int index = 0;
        int maxIndex = _gpx.Tracks.Length;
        Track current = _gpx.Tracks[index];
        Track previous = _gpx.Tracks[index];

        Console.WriteLine("Device is running...");
        
        try
        {
            OnTrackChange?.Invoke(0, current.DistanceInMeters);

            DateTime startTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                if (_totalDistanceM > current.TotalDistanceInMeters)
                {
                    if (_heartRate.Enabled) current.HeartRate = _heartRate.CurrentRate;

                    index++;

                    // update the statistics in the gpx file as accurate as possible
                    _ = Task.Run(() => UpdateGpxStatistics(index-1, maxIndex, startTime));

               
                    if (index >= maxIndex)
                    {
                        // Indicate the track change (this is just to set the progress bar to done :)
                        OnTrackChange?.Invoke(current.TotalDistanceInMeters,0);

                        Console.WriteLine("End of track reached.");
                        await _treadmill.StopAsync();
                        
                        // set the finish statistics
                        _playerStatistics.SegmentIncrementPercentage = 0;
                        _playerStatistics.SegmentRemainingM = 0;
                        _playerStatistics.SecondsElapsed = (DateTime.UtcNow - _startTime).TotalSeconds;

                        OnTrackReady?.Invoke(_playerStatistics);

                        break;
                    }
                    // set the new start time of the track    
                    startTime = DateTime.UtcNow;

                    //get the next track
                    current = _gpx.Tracks[index];
                    previous = _gpx.Tracks[index - 1];

                    // get the ascend and descend meters from the previous track
                    _playerStatistics.SegmentIncrementPercentage = current.InclinationInDegrees;
                    _playerStatistics.TotalInclinationM += previous.AscendInMeters;
                    _playerStatistics.TotalDeclinationM += previous.DescendInMeters;
                    _playerStatistics.CurrentSpeedKMH = 0;
                 
                    // Indicate the track change
                    OnTrackChange?.Invoke(previous.TotalDistanceInMeters, current.DistanceInMeters);

                    _ = AdjustTreadmillAsync(current, previous);
                }

                // ensure the remaining distance is updated for the segment
                _playerStatistics.SegmentRemainingM = current.TotalDistanceInMeters - _totalDistanceM;
                _playerStatistics.SecondsElapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
                
                await Task.Delay(1000, token);
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Background loop canceled.");
        }
        finally
        {
            Console.WriteLine("Device stopped.");
        }
    }

    private void UpdateGpxStatistics(int index, int maxIndex, DateTime startTime)
    {
        if (index >= maxIndex) return;

        int totalSeconds = (DateTime.UtcNow - startTime).Seconds;
        decimal totalDistance = _gpx.Tracks[index].DistanceInMeters;

        // Variabele to keep the current time
        DateTime currentSegmentTime = startTime;

        // Loop door de segmenten in de huidige track
        foreach (var segment in _gpx.Tracks[index].Segments)
        {
            // calculate the duration of the current segment based on its distance
            int segmentTimeInSeconds = (int)(segment.DistanceInMeters / totalDistance * totalSeconds);

            // Update time for current segment
            currentSegmentTime = currentSegmentTime.AddSeconds(segmentTimeInSeconds);

            // Update time in the GPX-segment
            var trackSegment = _gpx.Gpx.trk.trkseg[segment.GpxIndex];
            trackSegment.time = currentSegmentTime;

            // check if the extensions and heartrate extension exist and add the heart rate
            if (trackSegment.extensions == null) trackSegment.extensions = new();
            if (trackSegment.extensions.TrackPointExtension == null) trackSegment.extensions.TrackPointExtension = new();
            trackSegment.extensions.TrackPointExtension.hr = (byte)_gpx.Tracks[index].HeartRate;
        }
    }

    private async Task AdjustTreadmillAsync(Track track, Track? previousTrack)
    {
        short inclinationInDegrees = (short)Math.Round(track.InclinationInDegrees, 0);
        await _treadmill.ChangeInclineAsync(inclinationInDegrees);
        await Task.Delay(1000);
        await AdjustTreadmillSpeedAsync(track, previousTrack);
     
    }


    private async Task AdjustTreadmillSpeedAsync(Track track, Track? previousTrack)
    {
        if (_runSettings == null || !_runSettings.AutoSpeedControl)
            return;

        decimal newSpeed = Runtime.GetSpeed(track.InclinationInDegrees);
        decimal prevSpeed = previousTrack != null ? Runtime.GetSpeed(previousTrack.InclinationInDegrees) : newSpeed;

        int deltaIncline = (int)Math.Abs(track.InclinationInDegrees - (previousTrack?.InclinationInDegrees ?? 0));
        int delayMs = deltaIncline * 150;

        // bailout if current speed and current incline dont change
        if (deltaIncline == 0 && newSpeed == prevSpeed)
            return;

        await Task.Delay(delayMs);
        await _treadmill.ChangeSpeedAsync(newSpeed);
    }

    public void Dispose()
    {
        if (_treadmill != null)
        {
            _treadmill.OnStatisticsUpdate -= Treadmill_OnStatisticsUpdate;
            _treadmill.OnStatusUpdate -= Treadmill_OnStatusUpdate;
        }
        if (_heartRate != null)
        {
            _heartRate.OnHeartPulse -= HeartRate_OnHeartPulse;
        }
    }
}
