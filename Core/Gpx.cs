namespace Re_RunApp.Core;

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
[System.Xml.Serialization.XmlRoot(Namespace = "http://www.topografix.com/GPX/1/1", IsNullable = false)]
public partial class gpx
{
    private gpxMetadata metadataField;
    private gpxTrk trkField;
    private string creatorField;
    private decimal versionField;

    /// <remarks/>
    public gpxMetadata metadata
    {
        get
        {
            return metadataField;
        }
        set
        {
            metadataField = value;
        }
    }

    /// <remarks/>
    public gpxTrk trk
    {
        get
        {
            return trkField;
        }
        set
        {
            trkField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttribute()]
    public string creator
    {
        get
        {
            return creatorField;
        }
        set
        {
            creatorField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttribute()]
    public decimal version
    {
        get
        {
            return versionField;
        }
        set
        {
            versionField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
public partial class gpxMetadata
{

    private gpxMetadataLink linkField;

    private DateTime timeField;

    /// <remarks/>
    public gpxMetadataLink link
    {
        get
        {
            return linkField;
        }
        set
        {
            linkField = value;
        }
    }

    /// <remarks/>
    public DateTime time
    {
        get
        {
            return timeField;
        }
        set
        {
            timeField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
public partial class gpxMetadataLink
{

    private string textField;

    private string hrefField;

    /// <remarks/>
    public string text
    {
        get
        {
            return textField;
        }
        set
        {
            textField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttribute()]
    public string href
    {
        get
        {
            return hrefField;
        }
        set
        {
            hrefField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
public partial class gpxTrk
{

    private string nameField;

    private string typeField;

    private gpxTrkTrkpt[] trksegField;

    /// <remarks/>
    public string name
    {
        get
        {
            return nameField;
        }
        set
        {
            nameField = value;
        }
    }

    /// <remarks/>
    public string type
    {
        get
        {
            return typeField;
        }
        set
        {
            typeField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlArrayItem("trkpt", IsNullable = false)]
    public gpxTrkTrkpt[] trkseg
    {
        get
        {
            return trksegField;
        }
        set
        {
            trksegField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
public partial class gpxTrkTrkpt
{

    private decimal eleField;

    private DateTime timeField;

    private gpxTrkTrkptExtensions extensionsField;

    private double latField;

    private double lonField;

    /// <remarks/>
    public decimal ele
    {
        get
        {
            return eleField;
        }
        set
        {
            eleField = value;
        }
    }

    /// <remarks/>
    public DateTime time
    {
        get
        {
            return timeField;
        }
        set
        {
            timeField = value;
        }
    }

    /// <remarks/>
    public gpxTrkTrkptExtensions extensions
    {
        get
        {
            return extensionsField;
        }
        set
        {
            extensionsField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttribute()]
    public double lat
    {
        get
        {
            return latField;
        }
        set
        {
            latField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttribute()]
    public double lon
    {
        get
        {
            return lonField;
        }
        set
        {
            lonField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.topografix.com/GPX/1/1")]
public partial class gpxTrkTrkptExtensions
{

    private TrackPointExtension trackPointExtensionField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElement(Namespace = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1")]
    public TrackPointExtension TrackPointExtension
    {
        get
        {
            return trackPointExtensionField;
        }
        set
        {
            trackPointExtensionField = value;
        }
    }
}

/// <remarks/>
[Serializable()]
[System.ComponentModel.DesignerCategory("code")]
[System.Xml.Serialization.XmlType(AnonymousType = true, Namespace = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1")]
[System.Xml.Serialization.XmlRoot(Namespace = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1", IsNullable = false)]
public partial class TrackPointExtension
{

    private decimal atempField;

    private byte hrField;

    private byte cadField;

    /// <remarks/>
    public decimal atemp
    {
        get
        {
            return atempField;
        }
        set
        {
            atempField = value;
        }
    }

    /// <remarks/>
    public byte hr
    {
        get
        {
            return hrField;
        }
        set
        {
            hrField = value;
        }
    }

    /// <remarks/>
    public byte cad
    {
        get
        {
            return cadField;
        }
        set
        {
            cadField = value;
        }
    }
}