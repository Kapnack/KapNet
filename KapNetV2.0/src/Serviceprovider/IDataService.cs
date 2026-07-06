namespace ImageCampus.ToolBox.Services
{
	public interface IDataService : IService 
    {
        string ServiceReference { get; }
        object GetDataValue(string[] dataPath);
    }
}