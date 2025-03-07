using RestSharp;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MultipleSign.Sign
{
    public class Cloud189Consumer : ISignConsumer
    {
        public TaskItemEnum TaskItem => TaskItemEnum.Cloud189;

        public void LoadData(Conf conf, List<TaskData> tasks)
        {
            if (conf.Cloud189Conf != null && conf.Cloud189Conf.Accounts != null)
            {
                int idx = 1;
                foreach (var item in conf.Cloud189Conf.Accounts.Where(x => string.IsNullOrWhiteSpace(x.User) == false || string.IsNullOrWhiteSpace(x.Pwd) == false))
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
            if (taskData.Parameter is not Cloud189ConfModel cloud189ConfModel)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Parameter参数映射对象失败";
                return;
            }

            if (cloud189ConfModel.Ignore)
            {
                taskData.IsCompleted = false;
                taskData.Message = "Ignore跳过" + Environment.NewLine;
                return;
            }

            await DoSign(taskData, cloud189ConfModel, cancellationToken);
        }

        private async Task DoSign(TaskData taskData, Cloud189ConfModel cloud189ConfModel, CancellationToken cancellationToken)
        {
            StringBuilder sb = new();

            CloudClient cloudClient = new(cloud189ConfModel.User, cloud189ConfModel.Pwd);
            var responseDto = await cloudClient.Login(cancellationToken);
            if (responseDto.IsCompleted == false)
            {
                taskData.IsCompleted = responseDto.IsCompleted;
                taskData.Message = responseDto.Message;
                return;
            }

            await DoWork(cloudClient, sb, cancellationToken);

            await DoFamilyWork(cloudClient, sb, cancellationToken);

            taskData.IsCompleted = true;
            taskData.Message = sb.ToString();
        }

        private async Task DoWork(CloudClient cloudClient, StringBuilder sb, CancellationToken cancellationToken)
        {
            Random rd = new(Guid.NewGuid().GetHashCode());
            List<string> lst = [];

            var jObject1 = await cloudClient.UserSign(cancellationToken);
            bool isSign = jObject1?["isSign"]?.ToString()?.ToBool() ?? false;
            lst.Add($"{(isSign ? "已经签到过了，" : "")}签到获得 {(jObject1?["netdiskBonus"]?.ToString() ?? "")} M空间");

            await Task.Delay(rd.Next(4000, 5000), cancellationToken);
            var jObject2 = await cloudClient.TaskSign(cancellationToken);
            BuildTaskResult(jObject2, lst);

            await Task.Delay(rd.Next(4000, 5000), cancellationToken);
            var jObject3 = await cloudClient.TaskPhoto(cancellationToken);
            BuildTaskResult(jObject3, lst);

            lst.ForEach(x => sb.AppendLine(x));
        }

        private void BuildTaskResult(JsonObject jObject, List<string> lst)
        {
            int index = lst.Count;
            string errorCode = jObject?["errorCode"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(errorCode))
                lst.Add($"抽奖，第{index}次，成功，抽奖获得 {jObject?["prizeName"]?.ToString()}");
            else
            {
                if (errorCode == "User_Not_Chance")
                    errorCode = "次数不足";
                lst.Add($"抽奖，第{index}次，失败，{errorCode}");
            }
        }

        private async Task DoFamilyWork(CloudClient cloudClient, StringBuilder sb, CancellationToken cancellationToken)
        {
            var jObject = await cloudClient.GetFamilyList(cancellationToken);
            var attr = jObject?["familyInfoResp"];
            if (attr != null && attr is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                {
                    string familyId = item?["familyId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(familyId))
                        continue;

                    var familyObject = await cloudClient.FamilyUserSign(familyId, cancellationToken);
                    bool isSign = familyObject?["signStatus"]?.ToString()?.ToBool() ?? false;
                    sb.AppendLine($"家庭：{Util.DesensitizeStr(familyId)}，{(isSign ? "已经签到过了，" : "")}签到获得 {(familyObject?["bonusSpace"]?.ToString() ?? "")} M空间");
                }
            }
        }
    }

    public class CloudClient
    {
        private readonly static string clientId = "538135150693412";
        private readonly static string model = "KB2000";
        private readonly static string version = "9.0.6";
        private readonly static string rsa_public_key = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCZLyV4gHNDUGJMZoOcYauxmNEsKrc0TlLeBEVVIIQNzG4WqjimceOj5R9ETwDeeSN3yejAKLGHgx83lyy2wBjvnbfm/nLObyWwQD/09CmpZdxoFYCH6rdDjRpwZOZ2nXSZpgkZXoOBkfNXNxnN74aXtho2dqBynTw3NFTWyQl8BQIDAQAB";
        private readonly static string form_pre = "{NRP}";
        private readonly static Dictionary<string, string> _headers = new()
        {
            {"User-Agent",$"Mozilla/5.0 (Linux; U; Android 11; {model} Build/RP1A.201005.001) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/74.0.3729.136 Mobile Safari/537.36 Ecloud/{version} Android/30 clientId/{clientId} clientModel/{model} clientChannelId/qq proVersion/1.0.6" },
            {"Referer","https://m.cloud.189.cn/zhuanti/2016/sign/index.jsp?albumBackupOpened=1" },
            {"Accept-Encoding","gzip, deflate" },
            {"Host","cloud.189.cn" },
        };

        private string _username { get; set; }
        private string _pwd { get; set; }
        private string _accessToken = "";

        private CacheQuery _cacheQuery;
        private CookieContainer _cookieContainer;
        private HttpClientHandler _httpClientHandler;
        private HttpClient _client;
        private RestClient _restClient;

        public CloudClient(string username, string pwd)
        {
            _username = username;
            _pwd = pwd;

            _cacheQuery = new CacheQuery();
            _cookieContainer = new CookieContainer();
            _httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = _cookieContainer,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            _client = new HttpClient(_httpClientHandler);
            _restClient = new RestClient(_client);
        }

        private async Task<JsonObject> GetEncrypt(CancellationToken cancellationToken)
        {
            var url = "https://open.e.189.cn/api/logbox/config/encryptConf.do";
            RestRequest request = new(url) { Method = Method.Post, };

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
            /*返回值jObject
            {
                "result": 0,
                "data": {
                    "upSmsOn": "0",
                    "pre": "{NRP}",
                    "preDomain": "id6.me",
                    "pubKey": "MIGfMA0GCS"
                }
            }             
             */
        }

        private async Task<Uri> RedirectURL(CancellationToken cancellationToken)
        {
            var url = "https://cloud.189.cn/api/portal/loginUrl.action?redirectURL=https://cloud.189.cn/web/redirect.html?returnURL=/main.action";
            RestRequest request = new(url) { Method = Method.Get };

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            return response?.ResponseUri;
            /*返回值ResponseUri
             https://open.e.189.cn/api/logbox/separate/web/index.html?appId=cloud&lt=031CD&reqId=2dbea&encryptUrl=CCE90
             */
        }

        private async Task<JsonObject> AppConf(CancellationToken cancellationToken)
        {
            var url = "https://open.e.189.cn/api/logbox/oauth2/appConf.do";
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:74.0) Gecko/20100101 Firefox/76.0" },
                { "Referer", "https://open.e.189.cn/" },
                { "lt", _cacheQuery.lt},
                { "REQID", _cacheQuery.REQID },
            };
            var param = new Dictionary<string, string>
            {
                { "version", "2.0" },
                { "appKey", _cacheQuery.appId },
            };
            RestRequest request = new(url) { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            foreach (var item in param)
                request.AddOrUpdateParameter(item.Key, item.Value);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
            /*返回值jObject
            {
                "result": 0,
                "data": {
                    "returnUrl": "https://cloud.189.cn/api/portal/callbackUnify.action?",
                    "paramId": "9B4C28EF31",
                    ""...
                }
            }             
             */
        }

        private Dictionary<string, string> BuilLoginForm(JsonObject appConf, string pubKey, string pre)
        {
            var returnUrl = appConf?["data"]?["returnUrl"]?.ToString() ?? "";
            var paramId = appConf?["data"]?["paramId"]?.ToString() ?? "";

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pubKey), out var _);

            string Encrypt(string _s)
                => BitConverter.ToString(rsa.Encrypt(Encoding.UTF8.GetBytes(_s), RSAEncryptionPadding.Pkcs1)).Replace("-", "").ToLower();

            var dic = new Dictionary<string, string>
            {
                ["appKey"] = "cloud",
                ["accountType"] = "01",
                ["userName"] = $"{pre}{Encrypt(_username)}",
                ["password"] = $"{pre}{Encrypt(_pwd)}",
                ["validateCode"] = "",
                ["captchaToken"] = "",
                ["returnUrl"] = returnUrl,
                ["mailSuffix"] = "@189.cn",
                ["paramId"] = paramId,
            };

            return dic;
        }

        private string SortParameter(Dictionary<string, string> dic)
        {
            if (dic == null || dic.Count == 0)
                return string.Empty;

            string result = string.Join("&", dic
                .Select(x => $"{x.Key}={x.Value}")
                .OrderBy(x => x, new Cloud189StringComparer()));

            return result;
        }

        private string GetSignature(Dictionary<string, string> dic)
        {
            var parameter = SortParameter(dic);
            byte[] hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(parameter));

            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
                sb.Append(hashBytes[i].ToString("x2"));

            string hash = sb.ToString();

            return hash;
        }

        public async Task<ResponseDto> Login(CancellationToken cancellationToken)
        {
            string pubKey = rsa_public_key;
            string pre = form_pre;

            bool encryptByNet = false;
            if (encryptByNet)
            {
                var encrypt = await GetEncrypt(cancellationToken);
                pubKey = encrypt?["data"]?["pubKey"]?.ToString() ?? "";
                pre = encrypt?["data"]?["pre"]?.ToString() ?? "";
            }

            var redirectResponseUri = await RedirectURL(cancellationToken);
            var queryDic = Util.UriQueryToDic(redirectResponseUri);
            _cacheQuery.REQID = queryDic.DicTryGet("reqId");
            _cacheQuery.lt = queryDic.DicTryGet("lt");
            _cacheQuery.appId = queryDic.DicTryGet("appId");

            var appConf = await AppConf(cancellationToken);

            var dic = BuilLoginForm(appConf, pubKey, pre);

            var url = "https://open.e.189.cn/api/logbox/oauth2/loginSubmit.do";
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:74.0) Gecko/20100101 Firefox/76.0" },
                { "Referer", "https://open.e.189.cn/" },
                { "REQID", _cacheQuery.REQID },
                { "lt", _cacheQuery.lt},
            };
            var param = dic;
            RestRequest request = new(url) { Method = Method.Post };
            request.AddOrUpdateHeaders(headers);
            foreach (var item in param)
                request.AddOrUpdateParameter(item.Key, item.Value);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            var result = jObject?["result"]?.ToString() ?? "";
            if (result != "0")
            {
                return new ResponseDto { IsCompleted = false, Message = jObject?["msg"]?.ToString() ?? "获取登录地址失败", };
            }

            string toUrl = jObject?["toUrl"]?.ToString() ?? "";
            RestRequest loginRequest = new(toUrl) { Method = Method.Get };
            loginRequest.AddOrUpdateHeaders(_headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse loginResponse = await _restClient.ExecuteAsync(loginRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (loginResponse.IsSuccessStatusCode == false)
            {
                return new ResponseDto { IsCompleted = false, Message = "跳转到登录页失败", };
            }

            return new ResponseDto { IsCompleted = true, Message = "登录成功", };
        }

        private async Task<JsonObject> FetchAPI(string url, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);

            var headers = new Dictionary<string, string>();
            foreach (var item in _headers)
                headers.Add(item.Key, item.Value);
            headers.Remove("Host");
            headers.Add("Host", uri.Host);

            RestRequest request = new(url) { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
        }

        public async Task<JsonObject> GetUserSizeInfo(CancellationToken cancellationToken)
        {
            string url = "https://cloud.189.cn/api/portal/getUserSizeInfo.action";
            var headers = new Dictionary<string, string>
            {
                { "Accept", "application/json;charset=UTF-8" },
            };
            RestRequest request = new(url) { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
            /*
            {
                "res_code": 0,
                "res_message": "成功",
                "account": "123@189.cn",
                "cloudCapacityInfo": {
                    "freeSize": 1,
                    "mail189UsedSize": 0,
                    "totalSize": 1,
                    "usedSize": 1
                },
                "familyCapacityInfo": {
                    "freeSize": 1,
                    "totalSize": 1,
                    "usedSize": 1
                },
                "totalSize": 1
            }             
             */
        }

        public async Task<JsonObject> UserSign(CancellationToken cancellationToken)
        {
            return await FetchAPI($"https://cloud.189.cn/mkt/userSign.action?rand={Util.GetTimeStamp_Milliseconds()}&clientType=TELEANDROID&version={version}&model={model}", cancellationToken);
            /*
            {
                "userSignId": null,
                "userId": 123,
                "signTime": "2025-01-08T04:07:12.298+00:00",
                "netdiskBonus": 73,
                "isSign": true
            }             
             */
        }

        public async Task<JsonObject> TaskSign(CancellationToken cancellationToken)
        {
            return await FetchAPI($"https://m.cloud.189.cn/v2/drawPrizeMarketDetails.action?taskId=TASK_SIGNIN&activityId=ACT_SIGNIN", cancellationToken);
            /*
            {
                "prizeId": "SIGNIN_CLOUD_50M",
                "prizeName": "天翼云盘50M空间",
                "prizeGrade": 1,
                "prizeType": 4,
                "description": "1",
                "useDate": "2025-01-08 14:24:56",
                "userId": 123,
                "isUsed": 1,
                "activityId": "ACT_SIGNIN",
                "prizeStatus": 1,
                "showPriority": 1
            }             
             */
        }

        public async Task<JsonObject> TaskPhoto(CancellationToken cancellationToken)
        {
            return await FetchAPI($"https://m.cloud.189.cn/v2/drawPrizeMarketDetails.action?taskId=TASK_SIGNIN_PHOTOS&activityId=ACT_SIGNIN", cancellationToken);
            /*
            {
                "prizeId": "SIGNIN_CLOUD_50M",
                "prizeName": "天翼云盘50M空间",
                "prizeGrade": 1,
                "prizeType": 4,
                "description": "1",
                "useDate": "2025-01-08 14:25:02",
                "userId": 123,
                "isUsed": 1,
                "activityId": "ACT_SIGNIN",
                "prizeStatus": 1,
                "showPriority": 1
            }             
             */
        }

        public async Task<JsonObject> TaskKJ(CancellationToken cancellationToken)
        {
            return await FetchAPI($"https://m.cloud.189.cn/v2/drawPrizeMarketDetails.action?taskId=TASK_2022_FLDFS_KJ&activityId=ACT_SIGNIN", cancellationToken);
        }

        public async Task<JsonObject> GetUserBriefInfo(CancellationToken cancellationToken)
        {
            string url = "https://cloud.189.cn/api/portal/v2/getUserBriefInfo.action";
            RestRequest request = new(url) { Method = Method.Get };

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
            /*
            {
                "res_code": 0,
                "res_message": "成功",
                "sessionKey": "2ff9eb9b-1111-9999-1111-a7d8ea441e41",
                "userAccount": "123@189.cn",
                "nickname": "",
                "icon": "",
                "encryptAccount": "0123"
            }             
             */
        }

        private async Task<JsonObject> GetAccessTokenBySsKey(string sessionKey, CancellationToken cancellationToken)
        {
            string appkey = "600100422";
            string time = Util.GetTimeStamp_Milliseconds().ToString();
            var dic = new Dictionary<string, string>
            {
                { "sessionKey", sessionKey },
                { "Timestamp", time },
                { "AppKey", appkey }
            };
            string signature = GetSignature(dic);

            var url = $"https://cloud.189.cn/api/open/oauth2/getAccessTokenBySsKey.action?sessionKey={sessionKey}";
            var headers = new Dictionary<string, string>
            {
                { "Sign-Type", "1" },
                { "Signature",  signature },
                { "Timestamp", time },
                { "AppKey", appkey },
            };
            RestRequest request = new(url) { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
            /*
            {
                "expiresIn": 1738119905000,
                "accessToken": "CD82"
            }             
             */
        }

        private async Task<JsonObject> FetchFamilyAPI(string path, CancellationToken cancellationToken)
        {
            var uri = new Uri(path);
            var query = Util.UriQueryToDic(uri);
            string time = Util.GetTimeStamp_Milliseconds().ToString();

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                var jObject1 = await GetUserBriefInfo(cancellationToken);
                string sessionKey = jObject1?["sessionKey"]?.ToString();
                var jObject2 = await GetAccessTokenBySsKey(sessionKey, cancellationToken);
                string accessToken = jObject2?["accessToken"]?.ToString();
                _accessToken = accessToken;
            }

            var dic = new Dictionary<string, string>();
            foreach (var item in query)
                dic.Add(item.Key, item.Value);
            dic.Add("Timestamp", time);
            dic.Add("AccessToken", _accessToken);
            string signature = GetSignature(dic);

            var url = path;
            var headers = new Dictionary<string, string>
            {
                { "Sign-Type", "1" },
                { "Signature",  signature },
                { "Timestamp", time },
                { "Accesstoken", _accessToken },
                { "Accept", "application/json;charset=UTF-8" },
                { "TE", "trailers" },
            };
            RestRequest request = new(url) { Method = Method.Get };
            request.AddOrUpdateHeaders(headers);

            cancellationToken.ThrowIfCancellationRequested();
            RestResponse response = await _restClient.ExecuteAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            var jObject = response?.Content?.TryToObject<JsonObject>();
            return jObject;
        }

        public async Task<JsonObject> GetFamilyList(CancellationToken cancellationToken)
        {
            return await FetchFamilyAPI("https://api.cloud.189.cn/open/family/manage/getFamilyList.action", cancellationToken);
            /*
            {
                "familyInfoResp": [
                    {
                        "count": 1,
                        "createTime": "2019-01-07 13:57:05",
                        "expireTime": "2099-12-31 02:59:59",
                        "familyId": 123,
                        "remarkName": "1",
                        "type": 1,
                        "useFlag": 1,
                        "userRole": 1
                    }
                ]
            }             
             */
        }

        public async Task<JsonObject> FamilyUserSign(string familyId, CancellationToken cancellationToken)
        {
            return await FetchFamilyAPI($"https://api.cloud.189.cn/open/family/manage/exeFamilyUserSign.action?familyId={familyId}", cancellationToken);
            /*
            {
	            "bonusSpace": 67,
	            "signFamilyId": 123,
	            "signStatus": 1,
	            "signTime": "2025-01-08 16:00:50",
	            "userId": 123
            }             
             */
        }
    }

    public class Cloud189Conf
    {
        public List<Cloud189ConfModel> Accounts { get; set; }
    }
    public class Cloud189ConfModel : BaseConf
    {
        public string User { get; set; }
        public string Pwd { get; set; }
    }

    public class CacheQuery
    {
        public string REQID { get; set; }
        public string lt { get; set; }
        public string appId { get; set; }
    }

    public class Cloud189StringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            int orderX = GetOrder(x[0]);
            int orderY = GetOrder(y[0]);

            if (orderX == orderY)
                return string.Compare(x, y, StringComparison.Ordinal);
            return orderX.CompareTo(orderY);
        }

        private int GetOrder(char c)
        {
            if (char.IsDigit(c))
                return 0;
            else if (char.IsUpper(c))
                return 1;
            else if (char.IsLower(c))
                return 2;
            return 999;
        }
    }
}
