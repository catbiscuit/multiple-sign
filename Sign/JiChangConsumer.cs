using RestSharp;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class JiChangConsumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.JiChang;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.JiChangConf != null && conf.JiChangConf.Domains != null)
            {
                int domainIdx = 1;
                foreach (var confModel in conf.JiChangConf.Domains.Where(x => string.IsNullOrWhiteSpace(x.Domain) == false))
                {
                    string domain = confModel.Domain;
                    if (domain.StartsWith("https://"))
                        domain = domain["https://".Length..];
                    if (domain.EndsWith('/'))
                        domain = domain[..^1];

                    if (confModel != null && confModel.Accounts != null)
                    {
                        int idx = 1;
                        foreach (var item in confModel.Accounts)
                        {
                            tasks.Add(new TaskData()
                            {
                                TaskId = tasks.Count + 1,
                                Title = $"({domainIdx}){domain}-({idx}){Util.DesensitizeStr(item.Email)}",
                                TaskItemEnum = TaskItem,
                                TaskItemSort = idx,
                                Parameter = new JiChangDomainAccount
                                {
                                    Domain = confModel.Domain,
                                    Email = item.Email,
                                    Pwd = item.Pwd,
                                    Ignore = item.Ignore,
                                },
                            });
                            idx++;
                        }
                    }
                    domainIdx++;
                }
            }
        }

        public async Task Consumer(TaskData taskData, CancellationToken cancellationToken)
        {
            if (taskData.Parameter is not JiChangDomainAccount jiChangDomainAccount)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (jiChangDomainAccount.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, jiChangDomainAccount, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, JiChangDomainAccount jiChangDomainAccount, CancellationToken cancellationToken)
        {
            StringBuilder sb = new();

            var headers = new Dictionary<string, string>
            {
                { "origin", jiChangDomainAccount.Domain },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36" },
            };
            var param = new Dictionary<string, string>
            {
                { "email", jiChangDomainAccount.Email },
                { "passwd", jiChangDomainAccount.Pwd },
            };
            var client = new RestClient(jiChangDomainAccount.Domain, options => { options.CookieContainer = new CookieContainer(); });
            RestRequest loginRequest = new("/auth/login") { Method = Method.Post };
            loginRequest.AddOrUpdateHeaders(headers);
            loginRequest.AddHeader("Content-Type", "application/json");
            var body = JsonSerializer.Serialize(param);
            loginRequest.AddStringBody(body, DataFormat.Json);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse loginResponse = await client.ExecuteAsync(loginRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var loginJObject = loginResponse?.Content?.TryToObject<JsonObject>();
            sb.AppendLine("🖥 登录结果：" + (loginJObject?["msg"]?.ToString() ?? ""));

            if (loginResponse.IsSuccessStatusCode == false)
            {
                sb.AppendLine("🧧 签到结果：登录不成功未执行.");

                taskData.IsCompleted = false;
                taskData.Message = sb.ToString();
                return;
            }

            var checkinRequest = new RestRequest("/user/checkin", Method.Post);
            checkinRequest.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse checkinResponse = await client.ExecuteAsync(checkinRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var checkinJObject = checkinResponse?.Content?.TryToObject<JsonObject>();
            sb.AppendLine("🧧 签到结果：" + (checkinJObject?["msg"]?.ToString() ?? ""));

            taskData.IsCompleted = true;
            taskData.Message = sb.ToString();
        }
    }

    public class JiChangConf
    {
        public List<JiChangConfModel> Domains { get; set; }
    }

    public class JiChangConfModel
    {
        public string Domain { get; set; }
        public List<JiChangConfModelInfo> Accounts { get; set; }
    }
    public class JiChangConfModelInfo : BaseConf
    {
        public string Email { get; set; }
        public string Pwd { get; set; }
    }
    public class JiChangDomainAccount : BaseConf
    {
        public string Domain { get; set; }
        public string Email { get; set; }
        public string Pwd { get; set; }
    }
}
