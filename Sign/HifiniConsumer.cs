using RestSharp;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MultipleSign.Sign
{
    public class HifiniConsumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.Hifini;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.HifiniConf != null && conf.HifiniConf.Cookies != null)
            {
                int idx = 1;
                foreach (var item in conf.HifiniConf.Cookies.Where(x => string.IsNullOrWhiteSpace(x.Cookie) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item.Cookie)}",
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
            if (taskData.Parameter is not HifiniConfModel hifiniConfModel)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (hifiniConfModel.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, hifiniConfModel, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, HifiniConfModel hifiniConfModel, CancellationToken cancellationToken)
        {
            var content = await SignV1(hifiniConfModel.Cookie, cancellationToken);
            if (content.Contains("操作存在风险") && content.Contains("encryptedSign"))
            {
                string sign = string.Empty;
                string pattern = @"var sign = ""([a-f0-9]+)"";";
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        sign = match.Groups[1].Value;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(sign))
                {
                    taskData.IsCompleted = false;
                    taskData.Message = "未签到，操作存在风险且未能解析出sign";
                    return;
                }

                await Task.Delay(3000, cancellationToken);

                content = await SignV2(hifiniConfModel.Cookie, sign, cancellationToken);

                taskData.IsCompleted = true;
                taskData.Message = GetMessage(content);
                return;
            }
            else if (content.Contains("操作存在风险，请稍后重试。") && content.Contains("$.xpost(xn.url('sg_sign'), {'sign':  sign}"))
            {
                string sign = string.Empty;
                string pattern = @"var sign = ""([a-f0-9]+)"";";
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        sign = match.Groups[1].Value;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(sign))
                {
                    taskData.IsCompleted = false;
                    taskData.Message = "未签到，操作存在风险且未能解析出sign";
                    return;
                }

                await Task.Delay(3000, cancellationToken);

                content = await SignV3(hifiniConfModel.Cookie, sign, cancellationToken);

                taskData.IsCompleted = true;
                taskData.Message = GetMessage(content);
                return;
            }
            else
            {
                taskData.IsCompleted = true;
                taskData.Message = GetMessage(content);
                return;
            }
        }

        private async Task<string> SignV1(string cookie, CancellationToken cancellationToken)
        {
            var url = "https://www.hifini.com/sg_sign.htm";
            Dictionary<string, string> headers = new()
            {
                { "Cookie", cookie },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36" },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return response?.Content;
        }

        private async Task<string> SignV2(string cookie, string sign, CancellationToken cancellationToken)
        {
            string dynamicKey = GenerateDynamicKey();
            string encryptedSign = SimpleEncrypt(sign, dynamicKey);

            string url = "https://hifini.com/sg_sign.htm";
            var headers = new Dictionary<string, string>
            {
                { "authority", "hifini.com" },
                { "accept", "text/plain, */*; q=0.01" },
                { "accept-language", "zh-CN,zh;q=0.9" },
                { "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
                { "cookie", cookie },
                { "origin", "https://hifini.com" },
                { "referer", "https://hifini.com/sg_sign.htm" },
                { "sec-ch-ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"" },
                { "sec-ch-ua-mobile", "?0" },
                { "sec-ch-ua-platform", "\"Windows\"" },
                { "sec-fetch-dest", "empty" },
                { "sec-fetch-mode", "cors" },
                { "sec-fetch-site", "same-origin" },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36" },
                { "x-requested-with", "XMLHttpRequest" }
            };
            var param = new Dictionary<string, string>
            {
                { "sign", encryptedSign },
            };
            string body = Util.BodyUrlEncode(param);
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            request.AddParameter("text/plain", body, ParameterType.RequestBody);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return response?.Content;
        }

        private async Task<string> SignV3(string cookie, string sign, CancellationToken cancellationToken)
        {
            string url = "https://hifini.com/sg_sign.htm";
            var headers = new Dictionary<string, string>
            {
                { "authority", "hifini.com" },
                { "accept", "text/plain, */*; q=0.01" },
                { "accept-language", "zh-CN,zh;q=0.9" },
                { "content-type", "application/x-www-form-urlencoded; charset=UTF-8" },
                { "cookie", cookie },
                { "origin", "https://hifini.com" },
                { "referer", "https://hifini.com/sg_sign.htm" },
                { "sec-ch-ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"" },
                { "sec-ch-ua-mobile", "?0" },
                { "sec-ch-ua-platform", "\"Windows\"" },
                { "sec-fetch-dest", "empty" },
                { "sec-fetch-mode", "cors" },
                { "sec-fetch-site", "same-origin" },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36" },
                { "x-requested-with", "XMLHttpRequest" }
            };
            var param = new Dictionary<string, string>
            {
                { "sign", sign },
            };
            string body = Util.BodyUrlEncode(param);
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            request.AddParameter("text/plain", body, ParameterType.RequestBody);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return response?.Content;
        }

        private static string GenerateDynamicKey()
        {
            long current_time = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            int key_index = (int)(current_time / (5 * 60 * 1000)) % 5;
            string[] keys = { "HIFINI", "HIFINI_COM", "HIFINI.COM", "HIFINI-COM", "HIFINICOM" };
            string result = keys[key_index];
            return result;
        }

        private static string SimpleEncrypt(string input, string key)
        {
            StringBuilder result = new();
            for (int i = 0; i < input.Length; i++)
            {
                result.Append((char)(input[i] ^ key[i % key.Length]));
            }
            return result.ToString();
        }

        private static string GetMessage(string text)
        {
            string message = "";

            if (text.Contains("成功签到"))
            {
                message = "成功签到";
            }
            else if (text.Contains("今天已经签过啦"))
            {
                message = "今天已经签过啦";
            }
            else if (text.Contains("操作存在风险"))
            {
                message = "未签到，操作存在风险";
            }
            else if (text.Contains("维护中"))
            {
                message = "未签到，服务器正在维护";
            }
            else if (text.Contains("请完成验证"))
            {
                message = "未签到，需要手动滑块验证";
            }
            else if (text.Contains("行为存在风险"))
            {
                message = "未签到，极验geetest页面滑块验证";
            }
            else if (text.Contains("正在进行人机识别"))
            {
                message = "未签到，页面需要renji.js跳转验证";
            }
            else
            {
                message = "签到结果解析错误";
            }

            return message;
        }
    }

    public class HifiniConf
    {
        public List<HifiniConfModel> Cookies { get; set; }
    }
    public class HifiniConfModel : BaseConf
    {
        public string Cookie { get; set; }
    }
}
