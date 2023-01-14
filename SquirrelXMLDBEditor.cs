namespace XMLDBEditor
{
    public class XMLDBEditorRun
    {
        public void edit(string database, string password, string tag, string editor_bin_name, string subtag_or_null)
        {
            using(XMLDBEditorBase xmlbse = new XMLDBEditorBase(database, tag, password, editor_bin_name, subtag_or_null))
            {
                ScorpionConsoleReadWrite.ConsoleWrite.writeWarning("Opening two instances of the same databases for editing may create unwanted conflicts");
                xmlbse.start();
            }
            return;
        }
    }
}