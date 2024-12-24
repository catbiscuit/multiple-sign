using RestSharp;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class GiteeConsumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.Gitee;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.GiteeConf != null && conf.GiteeConf.Accounts != null)
            {
                int idx = 1;
                foreach (var item in conf.GiteeConf.Accounts.Where(x => string.IsNullOrWhiteSpace(x.AccessToken) == false || string.IsNullOrWhiteSpace(x.Owner) == false || string.IsNullOrWhiteSpace(x.Repo) == false || string.IsNullOrWhiteSpace(x.Path) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item.AccessToken)}",
                        TaskItemEnum = TaskItemEnum.Gitee,
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

            if (task.Parameter is GiteeConfModel giteeConfModel)
            {
                var msg = await DoSign(giteeConfModel);
                task.IsCompleted = true;
                task.Message = msg;
            }
            else
            {
                task.Message = "参数错误";
            }
        }

        private async Task<string> DoSign(GiteeConfModel giteeConfModel)
        {
            string content = Util.GetBeiJingTimeStr();
            string message = "1";
            var sha = await Sha(giteeConfModel);
            if (string.IsNullOrWhiteSpace(sha))
                return "git_sha 获取失败";

            var jObject = await Commit(giteeConfModel, content, sha, message);
            string res = jObject?["content"]?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(res))
                return "pull操作失败";
            else
                return res;
        }

        private async Task<string> Sha(GiteeConfModel giteeConfModel)
        {
            var url = "https://gitee.com/api/v5/repos/" + giteeConfModel.Owner + "/" + giteeConfModel.Repo + "/contents/" + giteeConfModel.Path + "?access_token=" + giteeConfModel.AccessToken;
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/json" },
                { "charset", "utf-8" },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);
            RestResponse response = await client.ExecuteAsync(request);
            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject?["sha"]?.ToString();
        }

        private async Task<JsonObject> Commit(GiteeConfModel giteeConfModel, string content, string sha, string message)
        {
            string url = "https://gitee.com/api/v5/repos/" + giteeConfModel.Owner + "/" + giteeConfModel.Repo + "/contents/" + giteeConfModel.Path;
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/json" },
                { "charset", "utf-8" },
            };
            var param = new Dictionary<string, string>
            {
                { "access_token", giteeConfModel.AccessToken },
                { "content", Convert.ToBase64String(Encoding.UTF8.GetBytes(content))},
                { "sha", sha },
                { "message",  message},
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Put };
            request.AddOrUpdateHeaders(headers);
            request.AddHeader("Content-Type", "application/json");
            var body = JsonSerializer.Serialize(param);
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = await client.ExecuteAsync(request);
            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
        }
    }

    public class GiteeConf
    {
        public List<GiteeConfModel> Accounts { get; set; }
    }
    public class GiteeConfModel
    {
        public string AccessToken { get; set; }
        public string Owner { get; set; }
        public string Repo { get; set; }
        public string Path { get; set; }
    }
}
