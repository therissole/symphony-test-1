namespace SymphonyTest1.Api.Features.Languages;

public static class LanguagesFeature
{
    public static RouteGroupBuilder MapLanguageEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Languages");

        ListLanguages.Map(group);
        GetLanguage.Map(group);
        CreateLanguage.Map(group);
        UpdateLanguage.Map(group);
        DeleteLanguage.Map(group);

        return group;
    }
}
