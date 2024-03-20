namespace Assets.Scripts.Game
{
    public enum FakeInputType : int
    {
        move,
        attack,
        disconnect
    }

    public class FakeInput
    {
        public FakeInputType _type { get; set; }
        public long _time_to_execute { get; set; }

        public FakeInput(FakeInputType type, long time)
        {
            _type = type;
            _time_to_execute = time;
        }
    }

    internal static class FakeInputContainer
    {
        private static FakeInput _input = null;

        public static bool Exist()
        {
            return (_input != null);
        }

        public static void PushInput(FakeInput input)
        {
            _input = input;
        }

        public static FakeInput? GetInput()
        {
            return _input;
        }

        public static void RemoveInput()
        {
            _input = null;
        }
    }
}
