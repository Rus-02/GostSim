public static class FixtureEventArgumentsExtensions
{
    public static int GetParentInstanceID(this FixtureEventArguments args)
    {
        if (args.ParentObject != null)
        {
            return args.ParentObject.GetInstanceID();
        }
        return 0;
    }
}
