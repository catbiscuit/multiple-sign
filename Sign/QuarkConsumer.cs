using RestSharp;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class QuarkConsumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.Quark;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.QuarkConf != null && conf.QuarkConf.Accounts != null)
            {
                int idx = 1;
                foreach (var item in conf.QuarkConf.Accounts.Where(x => string.IsNullOrWhiteSpace(x.User) == false || string.IsNullOrWhiteSpace(x.Kps) == false || string.IsNullOrWhiteSpace(x.Sign) == false || string.IsNullOrWhiteSpace(x.Vcode) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item.User)}",
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
            if (taskData.Parameter is not QuarkConfModel quarkConfModel)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (quarkConfModel.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, quarkConfModel, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, QuarkConfModel quarkConfModel, CancellationToken cancellationToken)
        {
            StringBuilder sb = new();
            var growth_info = await GetGrowthInfo(quarkConfModel, cancellationToken);
            if (growth_info != null && growth_info["data"] != null)
            {
                string v = "普通用户";
                if (bool.TryParse(growth_info["data"]["88VIP"]?.ToString(), out bool isVIP) && isVIP)
                    v = "88VIP";
                sb.AppendLine($"{v} {quarkConfModel.User}");

                long.TryParse(growth_info["data"]["total_capacity"]?.ToString(), out long total_capacity);
                long.TryParse(growth_info["data"]["cap_composition"]?["sign_reward"]?.ToString(), out long sign_reward);
                sb.AppendLine($"💾 网盘总容量：{ConvertBytes(total_capacity)}，签到累计容量：{ConvertBytes(sign_reward)}");

                if (bool.TryParse(growth_info["data"]["cap_sign"]?["sign_daily"]?.ToString(), out bool sign_daily) && sign_daily)
                {
                    long.TryParse(growth_info["data"]["cap_sign"]?["sign_daily_reward"]?.ToString(), out long sign_daily_reward);
                    int.TryParse(growth_info["data"]["cap_sign"]?["sign_progress"]?.ToString(), out int sign_progress);
                    int.TryParse(growth_info["data"]["cap_sign"]?["sign_target"]?.ToString(), out int sign_target);
                    sb.AppendLine($"🧧 签到日志: 今日已签到+{ConvertBytes(sign_daily_reward)}，连签进度({sign_progress + 1}/{sign_target})");
                }
                else
                {
                    var growth_sign = await GetGrowthSign(quarkConfModel, cancellationToken);
                    if (growth_sign != null && growth_sign["data"] != null)
                    {
                        long.TryParse(growth_sign["data"]["sign_daily_reward"]?.ToString(), out long sign_daily_reward);
                        int.TryParse(growth_info["data"]["cap_sign"]?["sign_progress"]?.ToString(), out int sign_progress);
                        int.TryParse(growth_info["data"]["cap_sign"]?["sign_target"]?.ToString(), out int sign_target);
                        sb.AppendLine($"🧧 执行签到: 今日已签到+{ConvertBytes(sign_daily_reward)}，连签进度({sign_progress + 1}/{sign_target})");
                    }
                    else
                    {
                        taskData.IsCompleted = false;
                        taskData.Message = "⛔ 签到异常: " + (growth_sign?["message"]?.ToString() ?? "");
                        return;
                    }
                }
            }
            else
            {
                taskData.IsCompleted = false;
                taskData.Message = "⛔ 签到异常: 获取成长信息失败";
                return;
            }

            taskData.IsCompleted = true;
            taskData.Message = sb.ToString();
        }

        private async Task<JsonObject> GetGrowthInfo(QuarkConfModel quarkConfModel, CancellationToken cancellationToken)
        {
            string url = "https://drive-m.quark.cn/1/clouddrive/capacity/growth/info";
            var query = new Dictionary<string, string>
            {
                { "pr", "ucpro" },
                { "fr", "android"},
                { "kps", quarkConfModel.Kps },
                { "sign", quarkConfModel.Sign },
                { "vcode", quarkConfModel.Vcode },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Get };
            foreach (var item in query)
                request.AddQueryParameter(item.Key, item.Value);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
        }

        private async Task<JsonObject> GetGrowthSign(QuarkConfModel quarkConfModel, CancellationToken cancellationToken)
        {
            string url = "https://drive-m.quark.cn/1/clouddrive/capacity/growth/sign";
            var query = new Dictionary<string, string>
            {
                { "pr", "ucpro" },
                { "fr", "android"},
                { "kps", quarkConfModel.Kps },
                { "sign", quarkConfModel.Sign },
                { "vcode", quarkConfModel.Vcode },
            };
            var param = new Dictionary<string, object>
            {
                { "sign_cyclic", true },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            foreach (var item in query)
                request.AddQueryParameter(item.Key, item.Value);
            request.AddHeader("Content-Type", "application/json");
            var body = JsonSerializer.Serialize(param);
            request.AddStringBody(body, DataFormat.Json);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
        }

        private static string ConvertBytes(long b)
        {
            if (b <= 0)
                return $"0 MB";

            double bytesToMB = b / (1024.0 * 1024.0);
            if (bytesToMB < 1000)
            {
                return $"{Math.Round(bytesToMB, 1)} MB";
            }
            else
            {
                double bytesToGB = bytesToMB / 1024.0;
                if (bytesToGB < 1000)
                {
                    return $"{Math.Round(bytesToGB, 1)} GB";
                }
                else
                {
                    double bytesToTB = bytesToGB / 1024.0;
                    return $"{Math.Round(bytesToTB, 1)} TB";
                }
            }
        }
    }

    public class QuarkConf
    {
        public List<QuarkConfModel> Accounts { get; set; }
    }
    public class QuarkConfModel : BaseConf
    {
        public string User { get; set; }
        public string Kps { get; set; }
        public string Sign { get; set; }
        public string Vcode { get; set; }
    }
}
