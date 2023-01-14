namespace XMLDBEditor
{
    public class XMLDBEditorRun
    {
        private bool started = false;
        XMLDBEditorBase xmlbse;
        public void edit(string database, string password, string tag, string editor_bin_name, string subtag_or_null)
        {
            if(!started)
            {
                using(xmlbse = new XMLDBEditorBase(database, tag, password, editor_bin_name, subtag_or_null))
                {
                    xmlbse.start();
                    started = true;
                }
            }
            else
                ScorpionConsoleReadWrite.ConsoleWrite.writeError("There is already an instance of the XMLDB editor running, close it in order to start a new one");
            return;
        }
    }
}