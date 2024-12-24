using RestSharp;
using System.Text;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class LinkAIConsumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.LinkAI;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.LinkAIConf != null && conf.LinkAIConf.Authorizations != null)
            {
                int idx = 1;
                foreach (var item in conf.LinkAIConf.Authorizations.Where(x => string.IsNullOrWhiteSpace(x) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item)}",
                        TaskItemEnum = TaskItemEnum.LinkAI,
                        TaskItemSort = idx,
                        Parameter = item,
                    });
                    idx++;
                }
            }
        }

        public async Task Consumer(TaskData task)
        {
            task.IsCompleted = false;

            if (task.Parameter is string token)
            {
                string[] parts = token.Split('.');
                if (parts.Length != 3)
                {
                    task.Message = "JWT 格式错误";
                    return;
                }

                var payloadJson = DecodeBase64Url(parts[1]);
                JsonObject payloadData = payloadJson.TryToObject<JsonObject>();
                var expValue = payloadData["exp"];

                if (expValue == null)
                {
                    task.Message = "JWT的exp不存在";
                    return;
                }

                if (((long)expValue) <= Util.GetTimeStamp_Seconds())
                {
                    task.Message = "请重新登录并更新Github中token的值！";
                    return;
                }

                var url = "https://link-ai.tech/api/chat/web/app/user/sign/in";
                Dictionary<string, string> headers = new()
                {
                    { "Authorization", "Bearer " + token },
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                    { "X-Forwarded-For", Util.GetFakeIP() },
                };
                var client = new RestClient(url);
                RestRequest request = new() { Method = Method.Get };
                request.AddOrUpdateHeaders(headers);
                RestResponse response = await client.ExecuteAsync(request);
                if (response.Content.Contains("今日已签到"))
                {
                    task.IsCompleted = true;
                    task.Message = "今日已签到，请明日再来！";
                }
                else if (response.Content.Contains("success"))
                {
                    task.IsCompleted = true;
                    task.Message = "签到成功！";
                }
                else if (response.Content.Contains("401"))
                {
                    task.IsCompleted = false;
                    task.Message = "jwt校验失败，请检查！";
                }
                else
                {
                    task.IsCompleted = false;
                    if (response.Content.Length > 50)
                        task.Message = response.Content[..50];
                    else
                        task.Message = "参数错误";
                }
            }
            else
            {
                task.Message = "参数错误";
            }
        }

        private static string DecodeBase64Url(string base64Url)
        {
            var base64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public class LinkAIConf
    {
        public List<string> Authorizations { get; set; }
    }
}
