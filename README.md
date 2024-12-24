# MultipleSign

## 一、Fork 仓库

## 二、添加 Secret

**`Settings`-->`Secrets and variables`-->`Actions`-->`Secrets`-->`New repository secret`，添加以下secrets：**
- `CONF`：其值如下：
    
	```json
	{
		"Bark_Devicekey": "xxx",//Bark推送，不使用的话填空
		"Bark_Icon": "https://xxx/logo_2x.png",//Bark推送的icon
		"Smtp_Server": "smtp.qq.com",
		"Smtp_Port": 587,
		"Smtp_Email": "xxx@qq.com",//Email推送，发送者的邮箱，不使用的话填空
		"Smtp_Password": "xxxx",
		"Receive_Email_List": [//Email推送接收者列表，为空时不发送
			"xxx@qq.com"
		],
		"MaxTaskDurationSeconds": 120,//每个任务执行的最大时长，单位：秒
		"ConsumerCount": 4,//并行执行的最大任务数
		"HifiniConf": {//Hifini
			"Cookies": [
				"bbs_sid=123"
			]
		},
		"LinkAIConf": {//LinkAI
			"Authorizations": [
				"eyJhbGciOi123.eyJzdWIiO123.7BWTpRa123"
			]
		},
		"QuarkConf": {//夸克
			"Accounts": [
				{
					"User": "xxx",
					"Kps": "AAS123",
					"Sign": "AAT123",
					"Vcode": "1721866879315"
				}
			]
		},
		"JiChangConf": {//Powered by SSPANEL构建的网站
			"Domains": [
				{
					"Domain": "https://xxx.one",
					"Accounts": [
						{
							"Email": "xxx@qq.com",
							"Pwd": "xxxx"
						}
					]
				}
			]
		},
		"GiteeConf": {//Gitee
			"Accounts": [
				{
					"AccessToken": "xxx",
					"Owner": "userxx",
					"Repo": "gitxx",
					"Path": "src/xx.json"
				}
			]
		}
	}
    ```

## 三、运行

**`Actions`->`Run`->`Run workflow`**

## 四、查看运行结果

**`Actions`->`Run`->`build`**

## 五、增加新的签到业务
1、TaskItemEnum 新增枚举

2、创建对应的入参类和字段，并在Conf类中添加字段

3、创建实现类，继承ISignConsumer，实现LoadData和Consumer
