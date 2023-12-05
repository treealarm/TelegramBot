using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLayerLib
{
  // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
  public class TrackPointsProto
  {
    public List<TrackPointProto> Tracks { get; set; }
  }

  public class ProtoCoord
  {
    public double Lat { get; set; }
    public double Lon { get; set; }
  }

  public class ProtoObjExtraProperty
  {
    public string StrVal { get; set; }
    public string PropName { get; set; }
    public string VisualType { get; set; }
  }

  public class ProtoGeoObject
  {
    public string Id { get; set; }
    public bool HasId { get; set; }
    public ProtoGeometry Location { get; set; }
    public int Radius { get; set; }
    public bool HasRadius { get; set; }
    public string ZoomLevel { get; set; }
    public bool HasZoomLevel { get; set; }
  }

  public class ProtoGeometry
  {
    public List<ProtoCoord> Coord { get; set; }
    public string Type { get; set; }
  }


  public class Timestamp
  {
    public int Seconds { get; set; }
    public int Nanos { get; set; }
  }

  public class TrackPointProto
  {
    public string Id { get; set; }
    public ProtoGeoObject Figure { get; set; }
    public Timestamp Timestamp { get; set; }
    public List<ProtoObjExtraProperty> ExtraProps { get; set; }
  }


}
