namespace ImageCampus.ToolBox.Pool
{
    public interface IResettable
    {
        void Assign(params object[] parameters);
        void Reset();
    }
}