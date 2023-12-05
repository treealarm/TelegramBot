using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net.Http.Headers;

public class OpenStreetMapImageGenerator
{
  public static async Task<MemoryStream> GenerateMapImageAsync(
    double topLeftLatitude,
    double topLeftLongitude,
    double bottomRightLatitude,
    double bottomRightLongitude,
    double centerLatitude,
    double centerLongitude,
    double radiusInMeters
    )
  {
    int zoom = 15; // Уровень масштабирования по умолчанию

    var topLeftTile = LatLongToTileXY(topLeftLatitude, topLeftLongitude, zoom);
    var bottomRightTile = LatLongToTileXY(bottomRightLatitude, bottomRightLongitude, zoom);

    var width = (bottomRightTile.X - topLeftTile.X + 1) * 256;
    var height = (bottomRightTile.Y - topLeftTile.Y + 1) * 256;

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
            image.Mutate(ctx => ctx.DrawImage(imageTile, new Point(tileX, tileY), 1f));
          }
        }
      }
    }

    var topLeft = TileXYToLatLong(topLeftTile, zoom);
    var bottomRight = TileXYToLatLong(new Point(bottomRightTile.X + 1, bottomRightTile.Y + 1), zoom);

    var pixelRadius = CalculatePixelRadius(topLeft, bottomRight, radiusInMeters, width, height);

    DrawCircle(image, topLeft, bottomRight, centerLongitude, centerLatitude, pixelRadius);
    image.SaveAsPng(@"M:\MapCash\output.png");

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
    var cacheDirectory = "./MapTilesCache/";

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



  private static void DrawCircle(Image<Rgba32> image, PointF topLeft, PointF bottomRight, double centerLongitude, double centerLatitude, float pixelRadius)
  {
    var centerX = (double)((centerLongitude - topLeft.X) / (bottomRight.X - topLeft.X) * image.Width);
    var centerY = (double)((centerLatitude - topLeft.Y) / (bottomRight.Y - topLeft.Y) * image.Height);

    var penColor = Color.Red.ToPixel<Rgba32>();
    var ellipse = new EllipsePolygon((float)centerX, (float)centerY, pixelRadius);
    image.Mutate(ctx => ctx.Draw(Color.Green, 2f, ellipse));
  }
}
