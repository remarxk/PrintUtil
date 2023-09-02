using System.Text;

public static class StringBuilderExtension
{
    public static string IndentStr = "  ";

    public static char GetLast(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return '\0';

        return sb[^1];
    }

    public static bool RemoveLast(this StringBuilder sb)
    {
        if (sb.Length == 0)
            return false;

        sb.Remove(sb.Length - 1, 1);
        return true;
    }

    public static void AppendLoop(this StringBuilder sb, string str, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sb.Append(str);
        }
    }

    public static void AppendLoop(this StringBuilder sb, char c, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sb.Append(c);
        }
    }

    public static void AppendIndent(this StringBuilder sb, int indent, string str)
    {
        sb.AppendLoop(IndentStr, indent);
        sb.Append(str);
    }

    public static void AppendIndent(this StringBuilder sb, int indent, char c)
    {
        sb.AppendLoop(c, indent);
        sb.Append(c);
    }
}