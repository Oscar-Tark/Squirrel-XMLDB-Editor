namespace XMLDBEditor
{
    public class XMLDBEditorRun : XMLDBEditorBase
    {
        public void edit(string database, string password, string tag, string editor_bin_name, string subtag_or_null)
        {
            EditingElement editing_element = new EditingElement(database, tag, password, editor_bin_name, subtag_or_null);
            editing_elements.Add(database, editing_element);
            editing_element.start();
            return;
        }

        public void save()
        {
            return;
        }
    }
}