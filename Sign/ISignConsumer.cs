namespace MultipleSign.Sign
{
    public interface ISignConsumer
    {
        TaskItemEnum TaskItem { get; }

        void LoadData(Conf conf, List<TaskData> tasks);

        Task Consumer(TaskData task);
    }
}
