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
                foreach (var item in conf.LinkAIConf.Authorizations.Where(x => string.IsNullOrWhiteSpace(x.Authorization) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item.Authorization)}",
                        TaskItemEnum = TaskItem,
                        TaskItemSort = idx,
                        Parameter = item,
                    });
                    idx++;
                }
            }
        }

        public async Task Consumer(TaskData taskData, CancellationToken cancellationToken)
        {
            if (taskData.Parameter is not LinkAIConfModel linkAIConfModel)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (linkAIConfModel.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, linkAIConfModel, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, LinkAIConfModel linkAIConfModel, CancellationToken cancellationToken)
        {
            string[] parts = linkAIConfModel.Authorization.Split('.');
            if (parts.Length != 3)
            {
                taskData.IsCompleted = false;
                taskData.Message = "JWT 格式错误";
                return;
            }

            var payloadJson = DecodeBase64Url(parts[1]);
            JsonObject payloadData = payloadJson.TryToObject<JsonObject>();
            var expValue = payloadData["exp"];

            if (expValue == null)
            {
                taskData.IsCompleted = false;
                taskData.Message = "JWT的exp不存在";
                return;
            }

            if (((long)expValue) <= Util.GetTimeStamp_Seconds())
            {
                taskData.IsCompleted = false;
                taskData.Message = "请重新登录并更新Github中token的值！";
                return;
            }

            var url = "https://link-ai.tech/api/chat/web/app/user/sign/in";
            Dictionary<string, string> headers = new()
            {
                { "Authorization", "Bearer " + linkAIConfModel.Authorization },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                { "X-Forwarded-For", Util.GetFakeIP() },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response.Content.Contains("今日已签到"))
            {
                taskData.IsCompleted = true;
                taskData.Message = "今日已签到，请明日再来！";
                return;
            }
            else if (response.Content.Contains("success"))
            {
                taskData.IsCompleted = true;
                taskData.Message = "签到成功！";
                return;
            }
            else if (response.Content.Contains("401"))
            {
                taskData.IsCompleted = false;
                taskData.Message = "jwt校验失败，请检查！";
                return;
            }
            else
            {
                taskData.IsCompleted = false;
                taskData.Message = response.Content.Length > 50 ? response.Content[..50] : response.Content;
                return;
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
        public List<LinkAIConfModel> Authorizations { get; set; }
    }
    public class LinkAIConfModel : BaseConf
    {
        public string Authorization { get; set; }
    }
}
