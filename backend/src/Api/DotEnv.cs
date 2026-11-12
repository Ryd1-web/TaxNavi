namespace TaxNavi.Api;

/// <summary>
/// Loads the repository's git-ignored <c>.env</c> (the single local secrets file) into environment
/// variables for <c>dotnet run</c>. Variables that are already set always win, so Docker, Render and
/// user secrets are unaffected. Looks in the working directory and up to four parents.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; dir != null && depth < 5; depth++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"', '\'');
                if (value.Length > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }
    }
}
