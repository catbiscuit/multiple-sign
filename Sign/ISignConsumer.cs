namespace MultipleSign.Sign
{
    public interface ISignConsumer
    {
        TaskItemEnum TaskItem { get; }

        void LoadData(Conf conf, List<TaskData> tasks);

        Task Consumer(TaskData taskData, CancellationToken cancellationToken);
    }

    public class BaseConf
    {
        /// <summary>
        /// 是否忽略
        /// </summary>
        public bool Ignore { get; set; }
    }
}
