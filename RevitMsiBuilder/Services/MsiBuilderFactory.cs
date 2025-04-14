namespace RevitMsiBuilder.Services;

public class MsiBuilderFactory
{
    public static IMsiBuilder CreateMsiBuilder()
    {
        // Here we could select different implementations based on configuration or other factors
        return new WixSharpMsiBuilder();
    }
}