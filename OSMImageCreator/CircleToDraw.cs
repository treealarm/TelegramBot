using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSMImageCreator
{
  public class CircleToDraw
  {
    public double centerLatitude { get; set; }
    public double centerLongitude { get; set; }
    public double radiusInMeters { get; set; } = 25;
    public uint color { get; set; } = 100;
    public required string from_id { get; set; }
    public DateTime Timestamp { get; set; }
  }
}
