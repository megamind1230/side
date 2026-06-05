using System;
using System.IO;

namespace NextLearn;

public static class Constants
{
    public static string DecksDir
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "NextLearn", "decks");
        }
    }
}
