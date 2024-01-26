using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class KMeansResult
{
  public List<Vector2> Centroids { get; set; }
  public List<List<Vector2>> Clusters { get; set; }
}

public class KMeansCluster
{
  public static KMeansResult Cluster(List<Vector2> points, int k, int maxIterations = 100)
  {
    if (points == null || points.Count == 0 || k <= 0)
      throw new ArgumentException("Invalid input parameters");

    Random random = new Random();
    List<Vector2> centroids = points.OrderBy(p => random.Next()).Take(k).ToList();

    for (int iteration = 0; iteration < maxIterations; iteration++)
    {
      List<List<Vector2>> clusters = new List<List<Vector2>>(k);
      for (int i = 0; i < k; i++)
        clusters.Add(new List<Vector2>());

      foreach (Vector2 point in points)
      {
        int nearestCentroidIndex = FindNearestCentroid(point, centroids);
        clusters[nearestCentroidIndex].Add(point);
      }

      List<Vector2> newCentroids = new List<Vector2>();
      for (int i = 0; i < k; i++)
      {
        if (clusters[i].Count > 0)
        {
          float newX = clusters[i].Average(p => p.X);
          float newY = clusters[i].Average(p => p.Y);
          newCentroids.Add(new Vector2(newX, newY));
        }
        else
        {
          // If a cluster is empty, place a random point as its centroid
          newCentroids.Add(points[random.Next(points.Count)]);
        }
      }

      if (CentroidsEqual(centroids, newCentroids))
      {
        // Convergence reached
        return new KMeansResult { Centroids = newCentroids, Clusters = clusters };
      }

      centroids = newCentroids;
    }

    // Maximum iterations reached
    return new KMeansResult { Centroids = centroids, Clusters = null };
  }

  private static int FindNearestCentroid(Vector2 point, List<Vector2> centroids)
  {
    double minDistance = double.MaxValue;
    int nearestCentroidIndex = -1;

    for (int i = 0; i < centroids.Count; i++)
    {
      double distance = Distance(point, centroids[i]);
      if (distance < minDistance)
      {
        minDistance = distance;
        nearestCentroidIndex = i;
      }
    }

    return nearestCentroidIndex;
  }

  private static double Distance(Vector2 point1, Vector2 point2)
  {
    float dx = point1.X - point2.X;
    float dy = point1.Y - point2.Y;
    return Math.Sqrt(dx * dx + dy * dy);
  }

  private static bool CentroidsEqual(List<Vector2> centroids1, List<Vector2> centroids2)
  {
    for (int i = 0; i < centroids1.Count; i++)
    {
      if (centroids1[i] != centroids2[i])
        return false;
    }
    return true;
  }

  public static List<List<Vector2>> GetClusters(List<Vector2> points, int k, int maxIterations = 100)
  {
    return Cluster(points, k, maxIterations)?.Clusters;
  }

  public static List<PolygonF> GetClusterPolygons(List<Vector2> points, int k, int maxIterations = 100)
  {
    KMeansResult result = Cluster(points, k, maxIterations);

    if (result != null)
    {
      List<PolygonF> polygons = new List<PolygonF>();

      for (int i = 0; i < k; i++)
      {
        List<Vector2> cluster = result.Clusters[i];
        PolygonF polygon = new PolygonF(cluster.ToArray());
        polygons.Add(polygon);
      }

      return polygons;
    }

    return null;
  }
}

public class PolygonF
{
  public List<Vector2> Points { get; }

  public PolygonF(params Vector2[] points)
  {
    Points = new List<Vector2>(points);
  }
}
