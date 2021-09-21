using System.Text;

public class SqlHelper
{
    public string BuildIdList(string[] ids)
    {
                    var idBuilder = new StringBuilder();
                    idBuilder.AppendFormat("'{0}'", ids[0]);
                    for (int i = 1; i < ids.Length; i++)
                        idBuilder.AppendFormat(", '{0}'", ids[i]);

        return idBuilder.ToString();
    }
}