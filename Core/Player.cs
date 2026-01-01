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

    public event Action<PlayerStatistics>? OnTrackReady;
    public event Action<decimal,decimal>? OnTrackChange; 
    public event Action<PlayerStatistics>? OnStatisticsUpdate;
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
            // Fix: Check for null before casting
            _totalDistanceM = e.DistanceM.HasValue ? (decimal)e.DistanceM.Value : 0;

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

            // Prevent recursive calls to StopAsync triggered by treadmill stop notifications.
            // Instead, cancel the player's background loop and mark as not playing.
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch { }

            _isPlaying = false;

            // notify the ui that the track is ready (stopped)
            OnTrackReady?.Invoke(_playerStatistics);
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

    private List<(DateTime time, int? hr)> _trackPointTimestamps = new();

    private async Task BackgroundLoop(CancellationToken token)
    {
        int index = 0;
        int maxIndex = _gpx.Tracks.Length;
        Track current = _gpx.Tracks[index];
        Track previous = _gpx.Tracks[index];

        // Safely get track points for cumulative distance calculation
        var trackPoints = _gpx.Gpx?.trk?.trkseg;
        var cumulativeDistances = trackPoints != null
            ? CalculateCumulativeDistances(trackPoints)
            : new List<double> { 0 };

        Console.WriteLine("Device is running...");
        
        try
        {
            OnTrackChange?.Invoke(0, current.DistanceInMeters);

            DateTime startTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {

                // Get the current distance from the treadmill telemetry
                double currentDistance = (double)(GetDistanceRan() ?? 0);

                // Determine the active track point
                int activeTrackPointIndex = GetActiveTrackPointIndex(currentDistance, cumulativeDistances);

                // Update the timestamp for the active track point
                if (_trackPointTimestamps.Count <= activeTrackPointIndex)
                {
                    _trackPointTimestamps.Add((DateTime.UtcNow, _playerStatistics.CurrentHeartRate));
                }

                if (_totalDistanceM > current.TotalDistanceInMeters)
                {
                    if (_heartRate.Enabled) current.HeartRate = _heartRate.CurrentRate;

                    index++;

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
            UpdateGpxStatistics();
            Console.WriteLine("Device stopped.");
        }
    }

    private void UpdateGpxStatistics()
    {
        // Map collected timestamps to track points
        var trackPoints = _gpx.Gpx?.trk?.trkseg;

        if (trackPoints == null)
            return;

        DateTime? lastAssigned = null;
        int assigned = 0;

        for (int i = 0; i < _trackPointTimestamps.Count && i < trackPoints.Length; i++)
        {
            var (time, hr) = _trackPointTimestamps[i];
            trackPoints[i].time = time;
            lastAssigned = time;
            assigned++;

            if (hr.HasValue)
            {
                if (trackPoints[i].extensions == null)
                    trackPoints[i].extensions = new gpxTrkTrkptExtensions();
                if (trackPoints[i].extensions.TrackPointExtension == null)
                    trackPoints[i].extensions.TrackPointExtension = new TrackPointExtension();
                // Safe to dereference now
                trackPoints[i].extensions.TrackPointExtension.hr = (byte)hr.Value;
            }
        }

        // If we didn't collect timestamps for trailing points, fill them forward monotonically
        if (assigned > 0)
        {
            DateTime fillTime = lastAssigned!.Value;
            for (int i = assigned; i < trackPoints.Length; i++)
            {
                // increment by 1 second to keep monotonic increasing times
                fillTime = fillTime.AddSeconds(1);
                trackPoints[i].time = fillTime;
            }
        }
        else if (trackPoints.Length > 0)
        {
            // No timestamps collected at all - set all points to now (monotonic)
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < trackPoints.Length; i++)
            {
                trackPoints[i].time = now.AddSeconds(i);
            }
        }

        // Ensure _gpx.Gpx and its nested properties are not null before accessing trk.trkseg
        if (_gpx.Gpx != null)
        {
            if (_gpx.Gpx.metadata == null) _gpx.Gpx.metadata = new gpxMetadata();
            if (_gpx.Gpx.trk != null && _gpx.Gpx.trk.trkseg != null && _gpx.Gpx.trk.trkseg.Length > 0)
            {
                _gpx.Gpx.metadata.time = trackPoints[0].time;
            }
            else
            {
                _gpx.Gpx.metadata.time = DateTime.UtcNow;
            }
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

        // the speed is always > 0 (nature of treadmill). 
        // if we don't have a previous track, we assume speed was < 0  before to force speed change
        decimal prevSpeed = -1;
        if (previousTrack != null)   prevSpeed = Runtime.GetSpeed(previousTrack.InclinationInDegrees);
        
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

    // Calculate cumulative distances for all track points in the track
    private List<double> CalculateCumulativeDistances(gpxTrkTrkpt[] trackPoints)
    {
        var cumulativeDistances = new List<double> { 0 }; // Start with 0 for the first point

        for (int i = 1; i < trackPoints.Length; i++)
        {
            // Calculate the distance between the current and previous track point
            double distance = (double)GpxProcessor.GetDistance(
                trackPoints[i - 1].lat, trackPoints[i - 1].lon,
                trackPoints[i].lat, trackPoints[i].lon);

            // Add the distance to the cumulative total
            cumulativeDistances.Add(cumulativeDistances[i - 1] + distance);
        }

        return cumulativeDistances;
    }

    
    // Determine the active track point based on the current distance
    private int GetActiveTrackPointIndex(double currentDistance, List<double> cumulativeDistances)
    {
        for (int i = 0; i < cumulativeDistances.Count - 1; i++)
        {
            // Check if the current distance falls between two cumulative distances
            if (currentDistance >= cumulativeDistances[i] && currentDistance < cumulativeDistances[i + 1])
            {
                return i;
            }
        }

        // If the distance exceeds the total, return the last track point
        return cumulativeDistances.Count - 1;
    }
}
