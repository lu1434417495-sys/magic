using Godot;

internal interface IContentResourceLoader
{
    T LoadCanonical<T>(string resourcePath)
        where T : Resource;
}
