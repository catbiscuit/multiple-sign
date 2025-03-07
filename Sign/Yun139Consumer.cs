using RestSharp;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class Yun139Consumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.Yun139;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.Yun139Conf != null && conf.Yun139Conf.Accounts != null)
            {
                int idx = 1;
                foreach (var item in conf.Yun139Conf.Accounts.Where(x => string.IsNullOrWhiteSpace(x.Token) == false || string.IsNullOrWhiteSpace(x.Phone) == false))
                {
                    tasks.Add(new TaskData()
                    {
                        TaskId = tasks.Count + 1,
                        Title = $"({idx})、{Util.DesensitizeStr(item.Phone)}",
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
            if (taskData.Parameter is not Yun139ConfModel yun139ConfModel)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (yun139ConfModel.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, yun139ConfModel, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, Yun139ConfModel yun139ConfModel, CancellationToken cancellationToken)
        {
            YunClient yunClient = new(yun139ConfModel.Token, yun139ConfModel.Phone);
            var responseDto = await yunClient.UserSign(cancellationToken);
            taskData.IsCompleted = responseDto.IsCompleted;
            taskData.Message = responseDto.Message;
        }
    }

    public class YunClient(string token, string phone)
    {
        private string token = token;
        private string phone = phone;

        private string _url = "https://caiyun.feixin.10086.cn";
        private static string _jwtTokenKey = "jwtToken";
        private Dictionary<string, string> _headers = new()
        {
            { "Authorization", $"Basic {token}" },
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36" },
            { "Content-Type", "application/json" },
            { "Accept", "*/*" },
            { _jwtTokenKey, "" },
        };
        private Dictionary<string, string> _cookies = new()
        {
            { _jwtTokenKey, "" },
        };

        private async Task<ResponseDto> FetchSSOToken(CancellationToken cancellationToken)
        {
            string url = "https://orches.yun.139.com/orchestration/auth-rebuild/token/v1.0/querySpecToken?client=app";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Basic {token}" },
                { "Content-Type", "application/json" },
                { "Accept", "*/*" },
                { "Host", "orches.yun.139.com" },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36" },
            };
            var param = new Dictionary<string, string>
            {
                { "account", phone },
                { "toSourceId", "001003" },
            };
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            request.AddHeader("Content-Type", "application/json");
            var body = JsonSerializer.Serialize(param);
            request.AddStringBody(body, DataFormat.Json);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            bool success = jObject?["success"]?.ToString()?.ToBool() ?? false;
            if (success)
                return new ResponseDto(true, jObject?["data"]?["token"]?.ToString());
            else
                return new ResponseDto(false, $"SSOToken失败：{(jObject?["message"]?.ToString() ?? "")}");

            /*
            {
	            "success": true,
	            "code": "0",
	            "message": "OK",
	            "data": {
		            "result": {
			            "resultCode": "0",
			            "resultDesc": "请求成功"
		            },
		            "resultCode": "104000",
		            "token": "STuid123"
	            }             
             */
        }

        private async Task<ResponseDto> FetchJWTToken(CancellationToken cancellationToken)
        {
            var responseDto = await FetchSSOToken(cancellationToken);
            if (responseDto.IsCompleted == false)
                return new ResponseDto(false, responseDto.Message);

            string url = $"https://caiyun.feixin.10086.cn:7071/portal/auth/tyrzLogin.action?ssoToken={responseDto.Message}";
            var client = new RestClient(url);
            RestRequest request = new() { Method = Method.Get };
            request.AddOrUpdateHeaders(_headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            string jwt_token = jObject?["result"]?["token"]?.ToString();

            if (string.IsNullOrWhiteSpace(jwt_token))
                return new ResponseDto(false, $"JWTToken失败：{(jObject?["message"]?.ToString() ?? "")}");

            _headers[_jwtTokenKey] = jwt_token;
            _cookies[_jwtTokenKey] = jwt_token;

            return new ResponseDto(true, "");

            /*
            {
	            "code": 0,
	            "result": {
		            "account": "VN=",
		            "areaCode": "123",
		            "provCode": "123",
		            "token": "eyJhbGci"
	            }
            }             
             */
        }

        public async Task<ResponseDto> UserSign(CancellationToken cancellationToken)
        {
            var responseDto = await FetchJWTToken(cancellationToken);
            if (responseDto.IsCompleted == false)
                return new ResponseDto(false, responseDto.Message);

            string checkSign_url = $"{_url}/market/signin/page/infoV2?client=mini";
            Uri uri = new(_url);
            CookieContainer cookieContainer = new();
            foreach (var cookie in _cookies)
                cookieContainer.Add(new Cookie(cookie.Key, cookie.Value, "/", uri.Host));
            var client = new RestClient(checkSign_url, options => options.CookieContainer = cookieContainer);
            RestRequest request = new() { Method = Method.Get };
            request.AddOrUpdateHeaders(_headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            string msg = jObject?["msg"]?.ToString();
            if (msg != "success")
                return new ResponseDto(false, $"签到失败：{msg}");

            bool isSign = jObject?["result"]?["todaySignIn"]?.ToString()?.ToBool() ?? false;
            if (isSign)
            {
                string total = jObject?["result"]?["total"]?.ToString();
                string signInPoints = jObject?["result"]?["signInPoints"]?.ToString();
                string signCount = jObject?["result"]?["signCount"]?.ToString();
                return new ResponseDto(true, $"🧧 今日已签到，获得 {signInPoints} 云朵，总共 {total} 云朵，本月签到 {signCount} 次");
            }

            return await Sign(cancellationToken);

            /*
			{
				"code": 0,
				"msg": "success",
				"result": {
					"todaySignIn": true,
					"total": 3,
					"canExchangeText": "",
					"signInPoints": 3,
					"signCount": 1,
					"provinceCode": "123",
					"toReceive": 0,
					"isGuide": 0,
					"time": 1736396519067,
					"maxType": "",
					"remind": 0
				}
			}             
             */
        }

        private async Task<ResponseDto> Sign(CancellationToken cancellationToken)
        {
            string signurl = $"{_url}/market/manager/commonMarketconfig/getByMarketRuleName?marketName=sign_in_3";
            Uri uri = new(_url);
            CookieContainer cookieContainer = new();
            foreach (var cookie in _cookies)
                cookieContainer.Add(new Cookie(cookie.Key, cookie.Value, "/", uri.Host));
            var client = new RestClient(signurl, options => options.CookieContainer = cookieContainer);
            RestRequest request = new() { Method = Method.Get };
            request.AddOrUpdateHeaders(_headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await client.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            string msg = jObject?["msg"]?.ToString();
            if (msg == "success")
                return new ResponseDto(true, "🧧 签到成功");
            else
                return new ResponseDto(false, $"sign失败：{msg}");

            /*
            {
	            "code": 0,
	            "msg": "success",
	            "result": {
		            "marketname": "sign_in_3",
		            "starttime": "2020-03-31T16:00:00.000+00:00",
		            "endtime": "2030-10-31T15:59:59.000+00:00",
		            "rule": "",
		            "appRule": "",
		            "miniRule": ""
	            }
            }             
             */
        }
    }

    public class Yun139Conf
    {
        public List<Yun139ConfModel> Accounts { get; set; }
    }
    public class Yun139ConfModel : BaseConf
    {
        public string Token { get; set; }
        public string Phone { get; set; }
    }
}
