# MultipleSign

## 一、Fork 仓库

## 二、添加 Secret

**`Settings`-->`Secrets and variables`-->`Actions`-->`Secrets`-->`New repository secret`，添加以下secrets：**
- `PAT`：
GitHub的token

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
				{
					"Cookie": "123",
					"Ignore": false
				}
			]
		},
		"LinkaiConf": {//LinkAI
			"Authorizations": [
				{
					"Authorization": "eyJhbGciOi123.eyJzdWIiO123.7BWTpRa123",
					"Ignore": true
				}
			]
		},
		"QuarkConf": {//夸克
			"Accounts": [
				{
					"User": "xxx",
					"Kps": "AAS123",
					"Sign": "AAT123",
					"Vcode": "1721866879315",
					"Ignore": false
				}
			]
		},
		"JiChangConf": {////Powered by SSPANEL构建的网站
			"Domains": [
				{
					"Domain": "https://xxx.one",
					"Accounts": [
						{
							"Email": "xxx@qq.com",
							"Pwd": "xxxx",
							"Ignore": false
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
					"Path": "src/xx.json",
					"Ignore": false
				}
			]
		},
		"Cloud189Conf": {//天翼云盘
			"Accounts": [
				{
					"User": "xxx",
					"Pwd": "xxx",
					"Ignore": false
				}
			]
		},
		"Yun139Conf": {//移动云盘
			"Accounts": [
				{
					"Token": "xxx",
					"Phone": "xxx",
					"Ignore": true
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

## 六、关于运行时间
cron.yml文件根据`- cron: '5 19 * * *'`，也就是北京时间03:05时，随机生成下次运行的时间

1、关于分钟
`$(($RANDOM % 40 + 10))`
区间：(0-39)+10

2、关于小时
`$(($RANDOM % 2 + 22))`
区间：(0-1)+22
也就是22点或23点

最终生成区间：22:10-22:49和23:10-23:49

对应北京时间：06:10-06:49和07:10-07:49