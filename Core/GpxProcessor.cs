namespace Re_RunApp.Core;

using System.Xml.Serialization;


internal class GpxProcessor
{
    public gpx? Gpx { get => _gpx; }
    public Track[] Tracks { get => _tracks; }

    private gpx? _gpx;
    private Track[] _tracks = [];
    private DateTime _startTime;

    private const decimal DISTANCE_OFFSET_START = 25;


    public decimal TotalDistanceInMeters { get; private set; } = 0;
    public decimal TotalElevationInMeters { get; private set; } = 0;

    public void LoadGpxData(string filePath)
    {
        _startTime = DateTime.UtcNow;

        try
        {
            XmlSerializer serializer = new(typeof(gpx));
            using (FileStream fileStream = new(filePath, FileMode.Open))
            using (StreamReader reader = new(fileStream))
            {
                _gpx = (gpx)serializer.Deserialize(reader);
            }
        }
        catch (Exception e)
        {
            _gpx = null;
            throw new Exception("No GPX data loaded", e);
        }
    }

    public void UpdateGpxData(decimal? distanceRan)
    {
        int maxIndex = -1;
        if (distanceRan.HasValue)
        {
            Track? track = FindTrack(distanceRan.Value);
            if (track != null && track.Segments != null)
            {
                TrackSegment segment = track.Segments.Where(s => s.DistanceInMeters <= distanceRan.Value).MaxBy(s => s.DistanceInMeters);
                if (segment != null)
                    maxIndex = segment.GpxIndex;
            }
        }

        if (maxIndex > -1 && _gpx != null)
        {
            _gpx.trk.trkseg = _gpx.trk.trkseg.Where((x, i) => i <= maxIndex).ToArray();
        }
    }


    private void OverwriteGpxMetaData()
    {
        if (_gpx == null) return;
        _gpx.creator = "Re-Run - running app";
        _gpx.metadata.link.href = "https://azurecodingarchitect.com";
        _gpx.metadata.time = _startTime;
        _gpx.metadata.link.text = "Azure Coding Architect";
        _gpx.trk.name = $"Re-Run: virtual run - {_gpx.trk.name}";
        _gpx.trk.type = "virtual running";
    }

    public Track[] GetRun()
    {
        if (_gpx == null) return [];

        List<Track> tracks = ProcessGpxSegments(_gpx);
        tracks = CreateNewTracks(tracks);
        tracks.Insert(0, GetIntroTrack(tracks[0].StartElevation)); //set the initial elevation
        _tracks = [.. tracks];

        Console.WriteLine($"Tracks created, total distance {_tracks[_tracks.Length - 1].TotalDistanceInMeters:F0}");
        
        TotalDistanceInMeters = _tracks[_tracks.Length - 1].TotalDistanceInMeters;

        return _tracks;
    }

    public string GetSerializedGpxData()
    {
        OverwriteGpxMetaData();

        XmlSerializer serializer = new(typeof(gpx));
        using (StringWriter writer = new())
        {
            serializer.Serialize(writer, _gpx);
            return writer.ToString();
        }
    }


    private Track GetIntroTrack(decimal startElevation)
    {
        return new Track
        {
            Index = -1,
            DistanceInMeters = DISTANCE_OFFSET_START,
            AscendInMeters = 0,
            DescendInMeters = 0,
            TotalDistanceInMeters = DISTANCE_OFFSET_START,
            InclinationInDegrees = 0,
            TrackStartDistanceInMeters = 0,
            PreviousTrackDescended = false,
            StartElevation = startElevation,
            EndElevation = startElevation,
            Segments = []
        };

    }


    private List<Track> ProcessGpxSegments(gpx gpxData)
    {
        TotalElevationInMeters = 0;
        List<Track> tracks = [];
        decimal totalRunDistance = DISTANCE_OFFSET_START;

        for (int index = 0; index < gpxData.trk.trkseg.Length; index++)
        {
            gpxTrkTrkpt point1 = gpxData.trk.trkseg[index == 0 ? 0 : index - 1];
            gpxTrkTrkpt point2 = gpxData.trk.trkseg[index];
            decimal distanceInMeters = GetDistance(point1.lat, point1.lon, point2.lat, point2.lon);
            decimal elevationInMeters = point2.ele - point1.ele;
            totalRunDistance += distanceInMeters;

            TotalElevationInMeters += elevationInMeters >= 0 ? elevationInMeters : 0;


            tracks.Add(new Track
            {
                Index = index,
                DistanceInMeters = distanceInMeters,
                AscendInMeters = elevationInMeters >= 0 ? elevationInMeters : 0,
                DescendInMeters = elevationInMeters < 0 ? elevationInMeters : 0,
                TotalDistanceInMeters = totalRunDistance,
                StartElevation = point1.ele,
                EndElevation = point2.ele
            });
        }
        return tracks;
    }

    private List<Track> CreateNewTracks(List<Track> tracks)
    {
        List<Track> newTracks = [];
        List<TrackSegment> segments = [];
        decimal totalDistance = 0;
        decimal totalAscend = 0;
        decimal totalDescend = 0;

        decimal startRunDistance = 0;
        int lastGpxIndex = 0;
        decimal totalRunDistance = DISTANCE_OFFSET_START;
        int newTracksIndex = 0;

        for (int index = 0; index < tracks.Count; index++)
        {
            totalDistance += tracks[index].DistanceInMeters;
            totalAscend += tracks[index].AscendInMeters;
            totalDescend += tracks[index].DescendInMeters;

            segments.Add(new TrackSegment
            {
                GpxIndex = index,
                DistanceInMeters = tracks[index].DistanceInMeters
            });

            if (totalDistance >= 100)
            {

                decimal startElevation = tracks[newTracksIndex == 0 ? 0 : lastGpxIndex + 1].StartElevation;
                decimal endElevation = tracks[index].EndElevation;

                // fix: calculate the inclination percentage based on the difference in height
                decimal inclinationPercentage = (endElevation - startElevation) / totalDistance * 100;

                startRunDistance = totalRunDistance;
                totalRunDistance += totalDistance;
                lastGpxIndex = index;

                newTracks.Add(new Track
                {
                    Index = newTracksIndex,
                    GpxLastIndex = lastGpxIndex,
                    DistanceInMeters = totalDistance,
                    AscendInMeters = totalAscend,
                    DescendInMeters = totalDescend,
                    TotalDistanceInMeters = totalRunDistance,
                    InclinationInDegrees = inclinationPercentage,
                    TrackStartDistanceInMeters = startRunDistance,
                    PreviousTrackDescended = false,
                    StartElevation = startElevation,
                    EndElevation = endElevation,
                    Segments = segments
                });
                // reset counters
                totalDistance = 0;
                totalAscend = 0;
                totalDescend = 0;
                segments = [];

                newTracksIndex++;
            }
        }

        // did the previous track descend?
        for (int i = 1; i < newTracks.Count; i++)
        {
            if (newTracks[i].EndElevation < newTracks[i].StartElevation)
            {
                if (newTracks[i - 1].EndElevation < newTracks[i - 1].StartElevation)
                {
                    newTracks[i].PreviousTrackDescended = true;
                }
            }
        }

        _tracks = [.. newTracks];
        return newTracks;
    }


    private static decimal GetDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371000; // Radius of the Earth in meters
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distance = R * c; // Distance in meters
        return (decimal)distance;
    }

    private static double ToRadians(double angle) => angle * (Math.PI / 180);

    public int FindMinimumElevation() => _gpx == null ? 0 : (int)_gpx.trk.trkseg.Min(t => t.ele);

    public int FindMaximumElevation() => _gpx == null ? 0 : (int)_gpx.trk.trkseg.Max(t => t.ele);

    public Track? FindTrack(decimal distance) => _gpx == null ? null : _tracks.FirstOrDefault(t => t.TrackStartDistanceInMeters <= distance && t.TotalDistanceInMeters > distance);

}
