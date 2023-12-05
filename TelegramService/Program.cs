namespace TelegramService
{
  internal class Program
  {
    static async Task Main(string[] args)
    {
     await  OpenStreetMapImageGenerator.GenerateMapImageAsync(
       55.969369767309274, 37.182615716947076,
       55.96320956229026, 37.195751515810755,
       //55.969714697225484, 37.19068886765664
       55.969705,37.190517
       , 50
       );

      return;
      var envVars = new Dictionary<string, string>();

      var environmentVariables = Environment.GetEnvironmentVariables();

      foreach (var variable in environmentVariables.Keys)
      {
        var key = variable.ToString().ToLower();

        if (
          key == "botid" ||
          key == "chatid")
        {
          envVars.Add(key, environmentVariables[variable].ToString());
        }        
      }

      var sender = new TelegramPoller(envVars["botid"], envVars["chatid"]);

      await sender.DoWork();
    }
  }
}