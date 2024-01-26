using Microsoft.Net.Http.Headers;
using OSMImageCreator;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net.Http.Headers;
using System.Numerics;

public class OpenStreetMapImageGenerator
{
  public static async Task<MemoryStream> GenerateMapImageAsync(
    double topLeftLatitude,
    double topLeftLongitude,
    double bottomRightLatitude,
    double bottomRightLongitude,
    List<CircleToDraw> circles,
    string filePath
    )
  {
    int zoom = 14; // Уровень масштабирования по умолчанию

    var topLeftTile = LatLongToTileXY(topLeftLatitude, topLeftLongitude, zoom);
    var bottomRightTile = LatLongToTileXY(bottomRightLatitude, bottomRightLongitude, zoom);

    var width = (bottomRightTile.X - topLeftTile.X + 1) * 256;
    var height = (bottomRightTile.Y - topLeftTile.Y + 1) * 256;

    while ((width > 1024 || height > 1024) && zoom >= 1)
    {
      topLeftTile = LatLongToTileXY(topLeftLatitude, topLeftLongitude, zoom);
      bottomRightTile = LatLongToTileXY(bottomRightLatitude, bottomRightLongitude, zoom);

      width = (bottomRightTile.X - topLeftTile.X + 1) * 256;
      height = (bottomRightTile.Y - topLeftTile.Y + 1) * 256;
    }
    var image = new Image<Rgba32>(width, height);


    for (int x = topLeftTile.X; x <= bottomRightTile.X; x++)
    {
      for (int y = topLeftTile.Y; y <= bottomRightTile.Y; y++)
      {
        var tile = await DownloadTileImageAsync(zoom, x, y);

        if (tile != null)
        {
          using (var imageTile = Image.Load<Rgba32>(tile))
          {
            var tileX = (x - topLeftTile.X) * 256;
            var tileY = (y - topLeftTile.Y) * 256;
            var newPoint = new Point(tileX, tileY);
            image.Mutate(ctx => ctx.DrawImage(imageTile, newPoint, 1f));
          }
        }
      }
    }

    var topLeft = TileXYToLatLong(topLeftTile, zoom);
    var bottomRight = TileXYToLatLong(new Point(bottomRightTile.X + 1, bottomRightTile.Y + 1), zoom);

    
    Dictionary<string,List<CircleToDraw>> map_circles = 
      new Dictionary<string,List<CircleToDraw>>();

    List<Vector2> points
      = new List<Vector2>();
    foreach (var c in circles)
    {
      var pixelRadius = CalculatePixelRadius(topLeft, bottomRight, c.radiusInMeters, width, height);
      var centerX = (double)((c.centerLongitude - topLeft.X) / (bottomRight.X - topLeft.X) * image.Width);
      var centerY = (double)((c.centerLatitude - topLeft.Y) / (bottomRight.Y - topLeft.Y) * image.Height);

      points.Add(new Vector2() { X = (float)centerX, Y = (float)centerY });

      DrawCircle(image, centerX, centerY, pixelRadius, c.color);

      if (map_circles.TryGetValue(c.from_id, out var circle_list) )
      {
        circle_list.Add(c);
      }
      else
      {
        var clist = new List<CircleToDraw>() { c };
        map_circles[c.from_id] = clist;
      }      
    }

    foreach(var kvp in map_circles)
    {
      CircleToDraw? prevCircle = null;
      var circleList = kvp.Value.OrderBy(i => i.Timestamp);

      foreach (var c in circleList)
      {
        if (prevCircle != null)
        {
          var centerX = (double)((prevCircle.centerLongitude - topLeft.X) / (bottomRight.X - topLeft.X) * image.Width);
          var centerY = (double)((prevCircle.centerLatitude - topLeft.Y) / (bottomRight.Y - topLeft.Y) * image.Height);

          var centerX2 = (double)((c.centerLongitude - topLeft.X) / (bottomRight.X - topLeft.X) * image.Width);
          var centerY2 = (double)((c.centerLatitude - topLeft.Y) / (bottomRight.Y - topLeft.Y) * image.Height);

          DrawLine(image,
            centerX, centerY,
            centerX2, centerY2,
            c.color);
        }
        prevCircle = c;
      }
    }

    {
      var cluster = KMeansCluster.Cluster(points, points.Count/10, 10);
      foreach (var poligon in cluster.Clusters)
      {
        DrawPolygon(image, poligon);
      }
    }
    image.SaveAsPng(filePath);

    var stream = new MemoryStream();
    image.SaveAsPng(stream);
    stream.Position = 0;
    return stream;
  }


  private static PointF TileXYToLatLong(Point point, int zoom)
  {
    double n = Math.PI - (2 * Math.PI * point.Y) / Math.Pow(2, zoom);
    var longitude = point.X / Math.Pow(2, zoom) * 360 - 180;
    var latitude = 180 / Math.PI * Math.Atan(Math.Sinh(n));

    return new PointF((float)longitude, (float)latitude);
  }

  private static Point LatLongToTileXY(double latitude, double longitude, int zoom)
  {
    var x = (int)Math.Floor((longitude + 180) / 360 * Math.Pow(2, zoom));
    var y = (int)Math.Floor((1 - Math.Log(Math.Tan(latitude * Math.PI / 180) + 1 / Math.Cos(latitude * Math.PI / 180)) / Math.PI) / 2 * Math.Pow(2, zoom));

    return new Point(x, y);
  }

  private static async Task<byte[]> DownloadTileImageAsync(int zoom, int x, int y)
  {
    var cacheDirectory = System.IO.Path.GetFullPath("./MapTilesCache");

    if (!Directory.Exists(cacheDirectory))
    {
      Directory.CreateDirectory(cacheDirectory);
    }
    var fileName = $"{zoom}_{x}_{y}.png";
    var filePath = System.IO.Path.Combine(cacheDirectory, fileName);

    if (File.Exists(filePath))
    {
      return await File.ReadAllBytesAsync(filePath);
    }
    else
    {
      var url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";

      try
      {
        using (var httpClient = new HttpClient())
        {
          var productValue = new ProductInfoHeaderValue("Mozilla", "1.0");
          var commentValue = new ProductInfoHeaderValue("(+http://www.leftfront.ru)");

          httpClient.DefaultRequestHeaders.Add(
              HeaderNames.UserAgent, productValue.ToString());
          httpClient.DefaultRequestHeaders.Add(
              HeaderNames.UserAgent, commentValue.ToString());

          var response = await httpClient.GetAsync(url);
          if (response.IsSuccessStatusCode)
          {
            var tileData = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(filePath, tileData);
            return tileData;
          }
          else
          {
            Console.WriteLine($"Error downloading tile image. Status code: {response.StatusCode}");
            return null;
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error downloading tile image: {ex.Message}");
        return null;
      }
    }
  }


  private static float CalculatePixelRadius(PointF topLeft, PointF bottomRight, double radiusInMeters, int imageWidth, int imageHeight)
  {
    // Получаем разницу широты и долготы области
    var latitudeDifference = Math.Abs(topLeft.Y - bottomRight.Y);
    var longitudeDifference = Math.Abs(topLeft.X - bottomRight.X);

    // Вычисляем радиус в пикселях
    var earthRadius = 6378137; // Радиус Земли в метрах
    var latitudinalRadius = (radiusInMeters / earthRadius) * (180 / Math.PI);
    var longitudinalRadius = latitudinalRadius / Math.Cos(topLeft.Y * Math.PI / 180);

    var pixelRadiusX = (float)(longitudinalRadius / longitudeDifference * imageWidth);
    var pixelRadiusY = (float)(latitudinalRadius / latitudeDifference * imageHeight);

    var pixelRadius = Math.Max(pixelRadiusX, pixelRadiusY);

    return pixelRadius;
  }

  private static void DrawCircle(Image<Rgba32> image,
    double centerX,
    double centerY,
    float pixelRadius,
    uint color
    )
  {
    //var penColor = Color.Red.ToPixel<Rgba32>();
    byte red = (byte)(color >> 11);
    byte green = (byte)((color >> 5) & 63);
    byte blue = (byte)(color & 31);

    red = (byte)(red * 255 / 31);
    green = (byte)(green * 255 / 63);
    blue = (byte)(blue * 255 / 31);
    var penColor = new Rgba32(red, green, blue);
    var ellipse = new EllipsePolygon((float)centerX, (float)centerY, pixelRadius);
    image.Mutate(ctx => ctx.Draw(penColor, 2f, ellipse));
  }

  private static void DrawLine(Image<Rgba32> image,
    double centerX,
    double centerY,
    double centerX2,
    double centerY2,
    uint color
  )
  {
    //var penColor = Color.Red.ToPixel<Rgba32>();
    byte red = (byte)(color >> 11);
    byte green = (byte)((color >> 5) & 63);
    byte blue = (byte)(color & 31);

    red = (byte)(red * 255 / 31);
    green = (byte)(green * 255 / 63);
    blue = (byte)(blue * 255 / 31);
    var penColor = new Rgba32(red, green, blue);
    image.Mutate(ctx => ctx.DrawLine(penColor, 1,
    [new PointF((float)centerX, (float)centerY),
      new PointF((float)centerX2, (float)centerY2)]));

  }

  private static void DrawPolygon(Image<Rgba32> image, List<Vector2> points)
  {
    var pts = points.Select(v => new PointF() { X = v.X, Y = v.Y }).ToArray();
    var fillColor = new Rgba32(100, 200, 100, 70);
    var penColor = Color.Green.ToPixel<Rgba32>();
    var pen = new SolidPen(penColor, 1);

    // Нарисуйте полигон на изображении
    image.Mutate(ctx => ctx.FillPolygon(new DrawingOptions(), 
      new SolidBrush(fillColor), 
      pts));

    image.Mutate(ctx =>
    {
      // Создайте форму полигона
      var polygonShape = new Polygon(new LinearLineSegment(pts));

      // Закрасьте форму полигона
      ctx.Fill(fillColor, polygonShape);

      // Нарисуйте контур полигона
      ctx.Draw(pen, polygonShape);
    });
  }

  static void CreateHeatmap(Image<Rgba32> image, List<Vector2> points, int pointRadius, float intensityScale)
  {
    foreach (var point in points)
    {
      // Определяем интенсивность тепловой карты в данной точке
      float intensity = CalculateIntensity(points, point, pointRadius, intensityScale);

      // Определяем цвет в зависимости от интенсивности
      var color = GetColorFromIntensity(intensity);

      //// Рисуем круг в данной точке с учетом интенсивности
      //image.Mutate(ctx => ctx.Draw(
      //  Color.FromRgba(color.R, color.G, color.B, color.A), 
      //  pointRadius * intensity, new EllipsePolygon(point, pointRadius)));

      // Рисуем эллипс в данной точке с учетом интенсивности
      image.Mutate(ctx => ctx.Draw(Color.FromRgba(color.R, color.G, color.B, color.A), 
        pointRadius * intensity, new EllipsePolygon(point, pointRadius)));

    }
  }

  static float CalculateIntensity(List<Vector2> points, Vector2 currentPoint, int pointRadius, float intensityScale)
  {
    float intensity = 0;

    foreach (var point in points)
    {
      float distance = Vector2.Distance(point, currentPoint);
      intensity += MathF.Exp(-distance * distance / (2 * pointRadius * pointRadius));
    }

    return intensity * intensityScale;
  }

  static Rgba32 GetColorFromIntensity(float intensity)
  {
    // Пример: Чем выше интенсивность, тем ярче красный цвет
    byte red = (byte)(intensity * 255);
    return new Rgba32(red, 0, 0);
  }

}
