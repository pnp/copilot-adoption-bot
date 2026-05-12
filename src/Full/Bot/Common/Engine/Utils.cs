using DataUtils;
using System.Reflection;

namespace Engine;

public class Utils
{
    public static string ReadResource(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        return ResourceUtils.ReadResource(assembly, resourcePath);
    }
}
