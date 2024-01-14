using DBLayerLib;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GrpcDaprClientLib
{
  public class Database2045
  {
    private static ConcurrentDictionary<string, TrackPointsProto> _dicChatId2Tracks = new ConcurrentDictionary<string, TrackPointsProto>();
    
    public static string MapCashAbsolutePath
    {
      get 
      {
        var path = Path.GetFullPath("./MapCash");
        return path; 
      }
    }

    public static string MapCashTilesAbsolutePath
    {
      get
      {
        var path = Path.Combine(MapCashAbsolutePath, "MapTilesCache");
        return path;
      }
    }

    private static string _dataPath = $"{MapCashAbsolutePath}/data.txt";
    public static void Deserialize()
    {
      try
      {
        CreateCashDir();
        var text = File.ReadAllText(_dataPath);
        _dicChatId2Tracks = JsonSerializer.Deserialize<ConcurrentDictionary<string, TrackPointsProto>>(text);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

    public static void CreateCashDir()
    {
      
      bool exists = Directory.Exists(MapCashAbsolutePath);
      if (!exists)
      {
        Directory.CreateDirectory(MapCashAbsolutePath);
      }        
    }
    public static void Serialize()
    {
      try
      {
        CreateCashDir();

        var jsonString = JsonSerializer.Serialize(_dicChatId2Tracks, new JsonSerializerOptions() { WriteIndented = true });
        File.WriteAllText(_dataPath, jsonString);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }
    public static bool UpdateTracks(TrackPointsProto figs)
    {
      foreach (var track in figs.Tracks)
      {
        var prop_chat = track.ExtraProps.Where(p => p.PropName == "chat.id").FirstOrDefault();

        if (prop_chat == null)
        {
          continue;
        }

        if (_dicChatId2Tracks.TryGetValue(prop_chat.StrVal, out var trackPoints))
        {
          while (trackPoints.Tracks.Count > 1000)
          {
            trackPoints.Tracks.RemoveAt(0);
          }
          trackPoints.Tracks.Add(track);
        }
        else
        {
          _dicChatId2Tracks[prop_chat.StrVal] = new TrackPointsProto();
          _dicChatId2Tracks[prop_chat.StrVal].Tracks = [track];
        }
      }
      return true;
    }

    public static TrackPointsProto? GetTracksByChatId(string chat_id)
    {
      if (_dicChatId2Tracks.TryGetValue(chat_id, out var trackPoints))
      {
        return trackPoints;
      }
      return null;
    }
  }
}