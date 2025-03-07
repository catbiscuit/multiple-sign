using MultipleSign.Sign;
using System.Collections.Concurrent;
using System.Text;

namespace MultipleSign
{
    internal class Program
    {
        private static readonly ConcurrentQueue<TaskData> taskQueue = new();
        private static readonly ConcurrentBag<ConsumerResult> results = [];
        private static int consumerCount = 4;
        private static int maxTaskDurationMilliseconds = 1000 * 60;
        private static readonly ConcurrentDictionary<TaskItemEnum, ISignConsumer> implementations = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine(Util.GetBeiJingTimeStr() + " - Start running...");

            await Run();

            Console.WriteLine(Util.GetBeiJingTimeStr() + " - End running...");

#if DEBUG
            Console.WriteLine("Program Run On Debug");
            Console.ReadLine();
#else
            Console.WriteLine("Program Run On Release");
#endif
        }

        static async Task Run()
        {
            Conf conf = Util.GetEnvValue("CONF")?.TryToObject<Conf>();
            if (conf == null)
            {
                Console.WriteLine("Configuration initialization failed");
                return;
            }

            if (conf.ConsumerCount > 0)
                consumerCount = conf.ConsumerCount;
            if (conf.MaxTaskDurationSeconds > 0)
                maxTaskDurationMilliseconds = conf.MaxTaskDurationSeconds * 1000;

            // 添加interface的实现
            var interfaceType = typeof(ISignConsumer);
            var implementingTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);
            foreach (var type in implementingTypes)
            {
                var specificImplementation = Activator.CreateInstance(type) as ISignConsumer;
                if (implementations.ContainsKey(specificImplementation.TaskItem))
                {
                    Console.WriteLine("implementingTypes Error");
                    return;
                }
                implementations.TryAdd(specificImplementation.TaskItem, specificImplementation);
            }

            // 构造任务
            List<TaskData> tasks = [];
            foreach (var implementation in implementations)
                implementation.Value.LoadData(conf, tasks);

            // 添加任务到队列
            foreach (var item in tasks)
                taskQueue.Enqueue(item);

            // 启动消费者
            var consumerTasks = new Task[consumerCount];
            for (int i = 0; i < consumerCount; i++)
                consumerTasks[i] = ConsumeTasksAsync();

            // 等待所有消费者完成
            await Task.WhenAll(consumerTasks);

            StringBuilder sb = new();
            int no = 1;
            foreach (var v in Enum.GetValues(typeof(TaskItemEnum)))
            {
                TaskItemEnum taskItemEnum = (TaskItemEnum)v;

                sb.AppendLine("");
                sb.AppendLine($"{no++}、{taskItemEnum}");

                if (tasks.Any(x => x.TaskItemEnum == taskItemEnum) == false)
                {
                    sb.AppendLine("无");
                    continue;
                }

                foreach (var item in tasks.Where(x => x.TaskItemEnum == taskItemEnum).OrderBy(x => x.TaskItemSort))
                {
                    bool isCompleted = false;
                    string message = "";
                    var r = results.FirstOrDefault(x => x.TaskId == item.TaskId);
                    if (r != null)
                    {
                        isCompleted = r.IsCompleted;
                        message = r.Message;
                    }

                    sb.AppendLine(item.Title);
#if DEBUG
                    sb.AppendLine($"  Result：{(isCompleted ? "Yes" : "No")} - {message}");
#else
                    sb.AppendLine($"  Result：{(isCompleted ? "✅" : "❌")} - {message}");
#endif
                }
            }

            string title = "聚合签到提醒";
            string content = sb.ToString();
            string topicName = "MultipleSign Remind Services";

#if DEBUG
            Console.WriteLine(content);
#endif

            Console.WriteLine("Send");
            SendUtil.SendEMail(conf.Smtp_Server, conf.Smtp_Port, conf.Smtp_Email, conf.Smtp_Password, conf.Receive_Email_List, title, content, topicName);
            await SendUtil.SendBark(conf.Bark_Devicekey, conf.Bark_Icon, title, content);
        }

        private static async Task ConsumeTasksAsync()
        {
            while (true)
            {
                if (taskQueue.TryDequeue(out TaskData taskData))
                {
                    var cts = new CancellationTokenSource(maxTaskDurationMilliseconds);

                    var consumerResult = new ConsumerResult()
                    {
                        TaskId = taskData.TaskId,
                        IsCompleted = taskData.IsCompleted,
                        Message = taskData.Message,
                    };

                    try
                    {
                        var task = ProcessTaskAsync(taskData, cts.Token);
                        await task;

                        consumerResult.IsCompleted = taskData.IsCompleted;
                        consumerResult.Message = taskData.Message;
                        results.Add(consumerResult);
                    }
                    catch (OperationCanceledException)
                    {
                        consumerResult.IsCompleted = false;
                        consumerResult.Message = "失败，引发了超时异常";
                        results.Add(consumerResult);
                    }
                    catch (Exception ex)
                    {
                        consumerResult.IsCompleted = false;
                        consumerResult.Message = "失败，" + (ex?.Message ?? "");
                        results.Add(consumerResult);
                    }
                    finally
                    {
                        cts?.Dispose();
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static async Task ProcessTaskAsync(TaskData taskData, CancellationToken cancellationToken)
        {
            Console.WriteLine(Util.GetBeiJingTimeStr() + " - " + taskData.TaskId.ToString().PadLeft(4, '0') + " - Entry Process");

            if (implementations.TryGetValue(taskData.TaskItemEnum, out ISignConsumer implementation))
                await implementation.Consumer(taskData, cancellationToken);
            else
                throw new ArgumentException("Unknown implementation type.");
        }
    }

    public enum TaskItemEnum
    {
        Quark,
        JiChang,
        Gitee,
        Hifini,
        LinkAI,
        Cloud189,
        Yun139,
    }

    public class Conf : SendConf
    {
        public int MaxTaskDurationSeconds { get; set; }
        public int ConsumerCount { get; set; }
        public HifiniConf HifiniConf { get; set; }
        public LinkAIConf LinkAIConf { get; set; }
        public QuarkConf QuarkConf { get; set; }
        public JiChangConf JiChangConf { get; set; }
        public GiteeConf GiteeConf { get; set; }
        public Cloud189Conf Cloud189Conf { get; set; }
        public Yun139Conf Yun139Conf { get; set; }
    }

    public class TaskData
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public TaskItemEnum TaskItemEnum { get; set; }
        public int TaskItemSort { get; set; }
        public object Parameter { get; set; }
        public bool IsCompleted { get; set; }
        public string Message { get; set; }
    }

    public class ConsumerResult
    {
        public int TaskId { get; set; }
        public bool IsCompleted { get; set; }
        public string Message { get; set; }
    }

    public class ResponseDto
    {
        public ResponseDto()
        {

        }
        public ResponseDto(bool isCompleted, string message)
        {
            IsCompleted = isCompleted;
            Message = message;
        }
        public bool IsCompleted { get; set; }
        public string Message { get; set; }
    }
}
