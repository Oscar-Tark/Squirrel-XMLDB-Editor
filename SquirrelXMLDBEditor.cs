namespace XMLDBEditor
{
    public class XMLDBEditorRun : XMLDBEditorBase
    {
        private EditingElement editing_element;
        bool started = false;

        public void edit(string database, string password, string tag, string editor_bin_name, string subtag_or_null)
        {
            editing_element = new EditingElement(database, tag, password, editor_bin_name, subtag_or_null);
            editing_elements.Add(database, editing_element);
            editing_element.start();
            started = true;
            return;
        }

        public void save()
        {
            editing_element.saveChanges();
            return;
        }

        public void newFile()
        {
            editing_element.newFile();
            return;
        }
    }
}