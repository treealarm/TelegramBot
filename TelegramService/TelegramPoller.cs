using DBLayerLib;
using GrpcDaprClientLib;
using OSMImageCreator;
using System.Text.Json;

namespace TelegramService
{
  internal class TelegramPoller: IDisposable
  {
    private string _botId;
    private string _chatId;
    Database2045 _database = new Database2045();
  

    private CancellationToken _cancellationToken = new CancellationToken();
    private bool _somethingChanged = false;

    private List<TelegramLocationRequest> _locationRequests = new List<TelegramLocationRequest>();
    private TelegramPoller() { }
    public TelegramPoller(string botId, string chatId)
    {
      _botId = botId;
      _chatId = chatId;
    }

    private void ProcessCallback(TelegramService.Result r)
    {
      var btnData = r.callback_query.data.Split(':');

      if (btnData.Length < 2)
      {
        return;
      }

      if (btnData[0] == "a")
      {
        // Aknowleged button.
        if (long.TryParse(btnData[1], out var lVal))
        {
          _somethingChanged = true;
        }
      }
    }

    private async Task ProcessPhoto(
      TelegramService.Result r,
      string botId,
      string chat_id
    )
    {
      var photo = r.message.photo.LastOrDefault();

      if (photo == null)
      {
        return;
      }

      TelegramSender sender = new TelegramSender();

      var replay = await sender.GetPhoto(botId, photo.file_id);

      if (string.IsNullOrEmpty(replay.fileNameRef))
      {
        Console.WriteLine("unable to read file");
        return;
      }
      string fileNameRef = replay.fileNameRef;

      var path = $"{photo.file_unique_id}_{fileNameRef}";

      File.WriteAllBytes(path, replay.data);

      var replayLoc = await RequestLocation(botId, chat_id);

      if (replayLoc != null)
      {
        _locationRequests.Add(new TelegramLocationRequest()
        {
          ImageFile = Path.GetFileName(path),
          SourceMessage = replayLoc.result
        });
      }
    }

    private void ProcessReplay(Result r)
    {
      var replay_to_message = r.message.reply_to_message;
      var srcMessage = _locationRequests
        .Where(m => m.SourceMessage.message_id == replay_to_message.message_id)
        .FirstOrDefault();

      if (srcMessage != null)
      {
        _locationRequests.Remove(srcMessage);

      }
    }

    private async Task ProcessCommand(
      Result r,
      string botId,
      string chat_id)
    {
      var command = r.message.text.Split('@')[0];

      if (command == @"/geo")
      {
        try
        {
          TelegramSender sender = new TelegramSender();

          var msg = new TelegramMessage()
          {
            bot_id = botId,
            chat_id = chat_id,
            text = ("Group view")
          };   

          List<CircleToDraw> circles = new List<CircleToDraw>();

          var tracks = Database2045.GetTracksByChatId(chat_id);

          if (tracks != null)
          {
            foreach (var track in tracks.Tracks)
            {
              var location = track.Figure.Location.Coord.FirstOrDefault();

              var from_id = track.ExtraProps.Where(p => p.PropName == "from.id").FirstOrDefault();
              uint color = 200;
              if (from_id != null)
              {
                color = (uint)Convert.ToUInt64(from_id.StrVal);
              }

              if (location == null) { continue; }
              circles.Add(new CircleToDraw() { centerLatitude = location.Lat, centerLongitude = location.Lon , color = color });
            }
          }

          Database2045.CreateCashDir();
          msg.photo = Path.Combine($"{Database2045.MapCashAbsolutePath}","output.png");

          if (circles.Count == 0)
          { 
            await OpenStreetMapImageGenerator.GenerateMapImageAsync(55.969369767309274, 37.182615716947076, 55.96320956229026, 37.195751515810755, circles, msg.photo);
            await sender.SendPhoto(msg);
            return;
          }
          double? latMin = circles.MinBy(l => l.centerLatitude)?.centerLatitude;
          double? lonMin = circles.MinBy(l => l.centerLongitude)?.centerLongitude;
          double? latMax = circles.MaxBy(l => l.centerLatitude)?.centerLatitude;
          double? lonMax = circles.MaxBy(l => l.centerLongitude)?.centerLongitude;

          await OpenStreetMapImageGenerator.GenerateMapImageAsync(latMax.Value, lonMin.Value, latMin.Value, lonMax.Value, circles, msg.photo);
          await sender.SendPhoto(msg);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }
      }
    }

    private async Task ProcessEditedMessage(Result r)
    {
      var edited_message = r.edited_message;
      
      if (edited_message?.location != null)
      {
        await GrpcSendPoint(edited_message);
      }
    }

    private async Task<TelegramSingleUpdate> RequestLocation(string botId, string chatId)
    {
      try
      {
        TelegramSender sender = new TelegramSender();

        var msg = new TelegramMessage()
        {
          bot_id = botId,
          chat_id = chatId,
          text = ("TelegramRequestLocation")
        };

        var reply_markup = new LocationMarkup()
        {
          keyboard = new List<List<KeyboardButton>>()
        };

        var btn = new KeyboardButton()
        {
          text = ("TelegramRequestLocation"),
          request_location = true
        };

        var btnList = new List<KeyboardButton>();
        btnList.Add(btn);
        reply_markup.keyboard.Add(btnList);
        msg.reply_markup = reply_markup;
        var replay = await sender.Send(msg);
        var deserializedClass = JsonSerializer.Deserialize<TelegramSingleUpdate>(replay);
        return deserializedClass;
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }

      return null;
    }

    public async Task DoWork()
    {
      TelegramSender sender = new TelegramSender();
      long offset = 0;

      while (!_cancellationToken.IsCancellationRequested)
      {
        await Task.Delay(5000);

        try
        {
          var replay = await sender.GetUpdates(_botId, offset);

          if (replay == null || !replay.ok || replay.result == null)
          {
            continue;
          }

          if (replay.result.Count == 0)
          {
            continue;
          }

          Console.WriteLine($"Received {replay.result.Count} updates");

          foreach (var r in replay.result)
          {
            offset = r.update_id + 1;

            if (r.callback_query != null && r.callback_query.data != null)
            {
              ProcessCallback(r);
            }
            if (r.edited_message != null)
            {
              await ProcessEditedMessage(r);
            }
            if (r.message != null)
            {
              if (r.message.photo != null)
              {
                await ProcessPhoto(r, _botId, _chatId);
              }

              if (r.message.location != null)
              {
                 await GrpcSendPoint(r.message);
              }

              if (r.message.reply_to_message != null)
              {
                ProcessReplay(r);
              }

              if (r.message.entities != null && 
                r.message.entities.Where(e => e.type == "bot_command").FirstOrDefault() != null)
              {
                await ProcessCommand(r, _botId, _chatId);
              }
            }
          }

          if (_somethingChanged)
          {
            // Send the event to clients.
            _somethingChanged = false;
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }        
      }
    }

    public void Dispose()
    {
    }

    public static int ConvertToUnixTimestamp(DateTime date)
    {
      DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
      TimeSpan diff = date.ToUniversalTime() - origin;
      return (int)Math.Floor(diff.TotalSeconds);
    }

    private async Task GrpcSendPoint(Message message)
    {
      var figs = new TrackPointsProto();
      var track = new TrackPointProto();
      figs.Tracks = new List<TrackPointProto> { track };
      figs.Tracks.Add(track);

      track.Figure = new ProtoGeoObject();
      var fig = track.Figure;

      fig.Location = new ProtoGeometry();

      fig.Location.Type = "Point";

      DateTime timestamp = DateTime.UtcNow;


      if (message.edit_date != null)
      {
        timestamp = DateTime.UnixEpoch.AddSeconds(message.edit_date.Value);
      }
      else if (message.date != null)
      {
        timestamp = DateTime.UnixEpoch.AddSeconds(message.date.Value);
      }


      track.Timestamp = new Timestamp() { Seconds = ConvertToUnixTimestamp (timestamp) };

      fig.Location = new ProtoGeometry() { Type = "Point" };
      fig.Location.Coord = new List<ProtoCoord>();
      fig.Location.Coord.Add(new ProtoCoord()
      {
        Lat = message.location.latitude,
        Lon = message.location.longitude
      });

      track.ExtraProps = new List<ProtoObjExtraProperty> {  };
      track.ExtraProps.Add(new ProtoObjExtraProperty()
      {
        PropName = "track_name",
        StrVal = "lisa_alert"
      });
      
      track.ExtraProps.Add(new ProtoObjExtraProperty()
      {
        PropName = "from.username",
        StrVal = message.from.username
      });
      track.ExtraProps.Add(new ProtoObjExtraProperty()
      {
        PropName = "from.id",
        StrVal = message.from.id.ToString()
      });
      track.ExtraProps.Add(new ProtoObjExtraProperty()
      {
        PropName = "chat.title",
        StrVal = message.chat.title
      });

      track.ExtraProps.Add(new ProtoObjExtraProperty()
      {
        PropName = "chat.id",
        StrVal = message.chat.id.ToString()
      });

      try
      {
        Database2045.UpdateTracks(figs);
      }
      catch(Exception ex) 
      {
        await Task.Delay(0);
        Console.WriteLine(ex.Message);
      }
    }
  }
}
