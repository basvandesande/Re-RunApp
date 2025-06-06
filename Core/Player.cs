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

    private CancellationTokenSource _cancellationTokenSource;

    public event Action<PlayerStatistics> OnTrackReady;
    public event Action<decimal> OnTrackChange; 
    public event Action<PlayerStatistics> OnStatisticsUpdate;
    private PlayerStatistics _playerStatistics = new();

    private decimal? _totalDistanceM = 0;
    private DateTime _startTime;


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
        if (_isPlaying)
        {
            _totalDistanceM = (decimal)e.DistanceM;

            _playerStatistics.TotalDistanceM = _totalDistanceM < _playerStatistics.TotalDistanceM ?
                                                _playerStatistics.TotalDistanceM + _totalDistanceM :
                                                _totalDistanceM;

            _playerStatistics.CurrentSpeedKMH = e.SpeedKMH;

            _playerStatistics.CurrentSpeedMinKM = _playerStatistics.CurrentSpeedKMH.HasValue && _playerStatistics.CurrentSpeedKMH > 0
                ? TimeSpan.FromMinutes(60 / (double)e.SpeedKMH)
                : null;
        }
    }

    private void Treadmill_OnStatusUpdate(string e)
    {
        Debug.WriteLine(e);
        if (e == "STOP" || e == "STOP KEY")
        {
            Console.WriteLine("Treadmill stopped.");
            Task.Run(async () => await StopAsync());
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
        await BackgroundLoop(_cancellationTokenSource.Token);
    }

    public async Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();
        _isPlaying = false;
        await _treadmill.StopAsync();
    }

    public decimal? GetDistanceRan() => _playerStatistics.TotalDistanceM;

    private async Task BackgroundLoop(CancellationToken token)
    {
        int index = 0;
        int maxIndex = _gpx.Tracks.Length;
        Track current = _gpx.Tracks[index];
        Track previous = _gpx.Tracks[index];

        Console.WriteLine("Device is running...");
        //OnStatisticsUpdate?.Invoke(_playerStatistics);

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
                    UpdateGpxStatistics(index, startTime);

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
                    _playerStatistics.CurrentSpeedMinKM = null;

                    // Indicate the track change
                    OnTrackChange?.Invoke(previous.TotalDistanceInMeters);

                    await AdjustTreadmillAsync(current, previous);
                }

                // ensure the remaining distance is updated for the segment
                _playerStatistics.SegmentRemainingM = current.TotalDistanceInMeters - _totalDistanceM;
                _playerStatistics.SecondsElapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
                
                // update the statistics
                OnStatisticsUpdate?.Invoke(_playerStatistics);

                await Task.Delay(500, token);
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

    private void UpdateGpxStatistics(int index, DateTime startTime)
    {
        int totalSeconds = (DateTime.UtcNow - startTime).Seconds;
        decimal totalDistance = _gpx.Tracks[index].DistanceInMeters;

        // loop thru the segments in the current track
        foreach (var segment in _gpx.Tracks[index].Segments)
        {
            // Get the duration per segment
            int segmentTimeInSeconds = (int)(segment.DistanceInMeters / totalDistance * totalSeconds);
            startTime = startTime.AddSeconds(segmentTimeInSeconds);
            _gpx.Gpx.trk.trkseg[segment.GpxIndex].time = startTime;
            
            // todo uitzoeken gaat nog fout
            //    _gpx.Gpx.trk.trkseg[segment.GpxIndex].extensions.TrackPointExtension.hr = (byte)_gpx.Tracks[index].HeartRate;
        }
    }

    private async Task AdjustTreadmillAsync(Track track, Track? previousTrack)
    {
        short inclinationInDegrees = (short)Math.Round(track.InclinationInDegrees, 0);
        await _treadmill.ChangeInclineAsync(inclinationInDegrees);
        await Task.Delay(2000);
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
            else
                speed = (decimal)_speedSettings.Speed13to15;
        
            await Task.Delay(delayMs);
            await _treadmill.ChangeSpeedAsync((decimal)_speedSettings.Speed0to5 + 0.5m);
        }
    }
}
