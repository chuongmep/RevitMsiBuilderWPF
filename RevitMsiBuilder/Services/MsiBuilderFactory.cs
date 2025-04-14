namespace RevitMsiBuilder.Services;

public class MsiBuilderFactory
{
    public static IMsiBuilder CreateMsiBuilder(ILogger logger)
    {
        // Here we could select different implementations based on configuration or other factors
        return new WixSharpMsiBuilder(logger);
    }
}