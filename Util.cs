using Microsoft.AspNetCore.WebUtilities;
using RestSharp;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MultipleSign
{
    public static class SendUtil
    {
        public static int SendEMail(string smtp_Server, int smtp_Port, string smtp_Email, string smtp_Password, List<string> receive_Email_List, string title, string content, string topicName)
        {
            if (string.IsNullOrWhiteSpace(smtp_Email) || string.IsNullOrWhiteSpace(smtp_Password) || receive_Email_List == null || receive_Email_List.Count == 0 || receive_Email_List.All(string.IsNullOrWhiteSpace))
            {
                Console.WriteLine("【EMail】RECEIVE_EMAIL_LIST is null");
                return 0;
            }

            MailAddress fromMail = new(smtp_Email, topicName);
            foreach (var item in receive_Email_List)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                MailAddress toMail = new(item);

                MailMessage mail = new(fromMail, toMail)
                {
                    IsBodyHtml = false,
                    Subject = title,
                    Body = content
                };

                SmtpClient client = new()
                {
                    EnableSsl = true,
                    Host = smtp_Server,
                    Port = smtp_Port,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtp_Email, smtp_Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                client.Send(mail);
            }

            Console.WriteLine("【EMail】Success");
            return 1;
        }

        public static async Task<int> SendBark(string bark_Devicekey, string bark_Icon, string title, string content)
        {
            if (string.IsNullOrWhiteSpace(bark_Devicekey))
            {
                Console.WriteLine("【Bark】BARK_DEVICEKEY is empty");
                return 0;
            }

            string url = "https://api.day.app/push";
            if (string.IsNullOrWhiteSpace(bark_Icon) == false)
                url = url + "?icon=" + bark_Icon;

            Dictionary<string, string> headers = new()
            {
                { "charset", "utf-8" }
            };

            Dictionary<string, object> param = new()
            {
                { "title", title },
                { "body", content },
                { "device_key", bark_Devicekey }
            };

            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            request.AddHeader("Content-Type", "application/json");
            var body = param.ToJson();
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = await client.ExecuteAsync(request);
            var res = response.Content;
            var jObject = res.TryToObject<JsonObject>();
            try
            {
                if (jObject == null)
                {
                    Console.WriteLine("【Bark】Send message to Bark Error");
                    return -1;
                }
                else
                {
                    if (int.TryParse(jObject["code"]?.ToString(), out int code) && code == 200)
                    {
                        Console.WriteLine("【Bark】Send message to Bark successfully");
                        return 1;
                    }
                    else
                    {
                        Console.WriteLine($"【Bark】Send Message Response.{jObject["text"]?.ToString()}");
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("【Bark】Send message to Bark Catch." + (ex?.Message ?? ""));
                return -1;
            }
        }
    }

    public static class Util
    {
        public static bool ToBool(this object data)
        {
            if (data == null)
                return false;

            bool? value = GetBool(data);

            if (value != null)
                return value.Value;

            return bool.TryParse(data.ToString(), out bool result) && result;
        }

        private static bool? GetBool(this object data)
        {
            return data.ToString().Trim().ToLower() switch
            {
                "0" => false,
                "1" => true,
                "是" => true,
                "否" => false,
                "yes" => true,
                "no" => false,
                "false" => false,
                "true" => true,
                _ => null,
            };
        }

        public static Dictionary<string, string> UriQueryToDic(Uri uri)
        {
            Dictionary<string, string> dic = [];

            var query = QueryHelpers.ParseQuery(uri.Query);
            foreach (var key in query.Keys)
                dic.Add(key, query[key]);

            return dic;
        }

        public static string DicTryGet(this Dictionary<string, string> dic, string key)
        {
            if (dic == null)
                return null;

            if (dic.Count == 0)
                return null;

            return dic.TryGetValue(key, out string value) ? value : null;
        }

        public static string DesensitizeStr(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return "";

            if (str.Length <= 8)
            {
                int ln = Math.Max((int)Math.Floor((double)str.Length / 3), 1);
                return str[..ln] + "**" + str[^ln..];
            }

            if (str.Length == 11 && str[0] == '1')
                return str[..3] + "**" + str[^2..];

            return str[..3] + "**" + str[^4..];
        }

        public static long GetTimeStamp_Seconds()
        {
            DateTime currentTime = DateTime.UtcNow;
            DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan elapsedTime = currentTime - unixEpoch;
            return (long)elapsedTime.TotalSeconds;
        }

        public static long GetTimeStamp_Milliseconds()
        {
            DateTime currentTime = DateTime.UtcNow;
            DateTime unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan elapsedTime = currentTime - unixEpoch;
            return (long)elapsedTime.TotalMilliseconds;
        }

        public static string GetFakeIP()
        {
            Random rd = new(Guid.NewGuid().GetHashCode());
            return $"233.{rd.Next(64, 117)}.{rd.Next(0, 255)}.{rd.Next(0, 255)}";
        }

        public static DateTime GetBeiJingTime()
        {
            DateTime nowUtc = DateTime.UtcNow;
            TimeZoneInfo beijingTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            DateTime nowBeiJing = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, beijingTimeZone);
            return nowBeiJing;
        }

        public static string GetBeiJingTimeStr()
        {
            var dt = GetBeiJingTime();
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string BodyUrlEncode(Dictionary<string, string> parameters) => string.Join("&", parameters.Select(p => WebUtility.UrlEncode(p.Key) + "=" + WebUtility.UrlEncode(p.Value)));

        public static T ToObject<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
        }

        public static T TryToObject<T>(this string json)
        {
            try
            {
                return json.ToObject<T>();
            }
            catch
            {
                return default;
            }
        }

        public static string ToJson(this object obj)
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new DateTimeConverterUsingDateTimeFormat("yyyy-MM-dd HH:mm:ss")
                }
            };

            return JsonSerializer.Serialize(obj, options);
        }

        public static string GetEnvValue(string key)
        {
            string str = Environment.GetEnvironmentVariable(key);

#if DEBUG
            if (string.IsNullOrWhiteSpace(str))
            {
                string osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription?.ToLower() ?? "";
                string win = "windows".ToLower();
                if (osDescription.Contains(win))
                {
                    string f = @"D:\VSCodeSpace\MultipleSign-Local\sign.json";
                    if (File.Exists(f))
                    {
                        str = File.ReadAllText(f, System.Text.Encoding.UTF8);
                    }
                }
            }
#endif

            return str;
        }
    }

    public class DateTimeConverterUsingDateTimeFormat : JsonConverter<DateTime>
    {
        private readonly string _format;

        public DateTimeConverterUsingDateTimeFormat(string format)
        {
            _format = format;
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), _format, null);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(_format));
        }
    }

    public class SendConf
    {
        public string Bark_Devicekey { get; set; }
        public string Bark_Icon { get; set; }
        public string Smtp_Server { get; set; }
        public int Smtp_Port { get; set; }
        public string Smtp_Email { get; set; }
        public string Smtp_Password { get; set; }
        public List<string> Receive_Email_List { get; set; }
    }
}
