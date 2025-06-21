namespace Re_RunApp.Core;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

internal class Player
{
    private readonly GpxProcessor _gpx;
    private readonly ITreadmill _treadmill;
    private readonly IHeartRate _heartRate;
    private readonly SpeedSettings? _speedSettings;
    private bool _isPlaying = false;

    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<PlayerStatistics> OnTrackReady;
    public event Action<decimal> OnTrackChange; 
    public event Action<PlayerStatistics> OnStatisticsUpdate;
    private PlayerStatistics _playerStatistics = new();

    private decimal? _totalDistanceM = 0;
    private DateTime _startTime;
    private DateTime _lastStatisticsUpdate = DateTime.MinValue;
    private static readonly TimeSpan StatisticsUpdateInterval = TimeSpan.FromSeconds(5);


    public Player(GpxProcessor gpx, ITreadmill treadmill, IHeartRate heartRate, SpeedSettings speedSettings)
    {
        _gpx = gpx ?? throw new Exception("GpxProcessor is null");
        _speedSettings = speedSettings;

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
        if ((now - _lastStatisticsUpdate) < StatisticsUpdateInterval)
            return; // Ignore events that come in too quickly

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
            OnTrackChange?.Invoke(0);

            DateTime startTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                if (_totalDistanceM > current.TotalDistanceInMeters)
                {
                    if (_heartRate.Enabled) current.HeartRate = _heartRate.CurrentRate;

                    // update the statistics in the gpx file as accurate as possible
                    _ = Task.Run(() => UpdateGpxStatistics(index, maxIndex, startTime));

                    index++;

                    if (index >= maxIndex)
                    {
                        // Indicate the track change
                        OnTrackChange?.Invoke(current.TotalDistanceInMeters);

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
                    OnTrackChange?.Invoke(previous.TotalDistanceInMeters);

                    _ = AdjustTreadmillAsync(current, previous);
                }

                // ensure the remaining distance is updated for the segment
                _playerStatistics.SegmentRemainingM = current.TotalDistanceInMeters - _totalDistanceM;
                _playerStatistics.SecondsElapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
                
                // update the statistics
                OnStatisticsUpdate?.Invoke(_playerStatistics);

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

        // loop thru the segments in the current track
        foreach (var segment in _gpx.Tracks[index].Segments)
        {
            // Get the duration per segment
            int segmentTimeInSeconds = (int)(segment.DistanceInMeters / totalDistance * totalSeconds);
            startTime = startTime.AddSeconds(segmentTimeInSeconds);
            _gpx.Gpx.trk.trkseg[segment.GpxIndex].time = startTime;
            
            // ik denk dat hier het trackpointextension opject nog null is, check de gpx file init
            //    _gpx.Gpx.trk.trkseg[segment.GpxIndex].extensions.TrackPointExtension.hr = (byte)_gpx.Tracks[index].HeartRate;
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
        if (_speedSettings != null && _speedSettings.AutoSpeedControl)
        {
            // calculate the delay based on the inclination difference and ascend / descend
            int deltaIncline = (int)Math.Abs(track.InclinationInDegrees - (previousTrack?.InclinationInDegrees ?? 0));
            if (deltaIncline == 0) return;
            
            decimal speed = 0;
            int  delayMs = deltaIncline * 150;
            
            // set the speeds
            if (track.InclinationInDegrees < 0)
                 speed = (decimal)_speedSettings.Speed0to5 + 0.5m;
            if (track.InclinationInDegrees <= 5)
                 speed = (decimal)_speedSettings.Speed0to5;
            else if (track.InclinationInDegrees <= 8)
                speed = (decimal)_speedSettings.Speed6to8;
            else if (track.InclinationInDegrees <= 10)
                speed = (decimal)_speedSettings.Speed8to10;
            else if (track.InclinationInDegrees <= 12)
                speed = (decimal)_speedSettings.Speed11to12;
            else if (track.InclinationInDegrees <= 15)
                speed = (decimal)_speedSettings.Speed13to15;
            else
                speed = (decimal)_speedSettings.Speed13to15 - 0.5m;

            await Task.Delay(delayMs);
            await _treadmill.ChangeSpeedAsync(speed);
        }
    }
}
