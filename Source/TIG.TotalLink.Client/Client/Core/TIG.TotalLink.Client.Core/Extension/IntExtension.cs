namespace TIG.TotalLink.Client.Core.Extension
{
    public static class IntExtension
    {
        /// <summary>
        /// Returns the plural form of the specified word if the value is not equal to 1.
        /// </summary>
        /// <param name="i">A value used to determine if the word should be pluralized.</param>
        /// <param name="word">The word to pluralize.</param>
        /// <returns>The plural form of the specified word if the value is not equal to 1; otherwise returns the word unchanged.</returns>
        public static string Pluralize(this int i, string word)
        {
            if (i == 1)
                return word;

            return word.Pluralize();
        }
    }
}
