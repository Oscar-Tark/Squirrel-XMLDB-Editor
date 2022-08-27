namespace XMLDBEditor
{
    public class XMLDBEditor : XMLDBEditorBase
    {

        public void edit(string database, string tag, string subtag_or_null, string prefferred_editor_no_path)
        {
            EditingElement editing_element = new EditingElement(database, tag, prefferred_editor_no_path, subtag_or_null);
            editing_elements.Add(database, editing_element);
            editing_element.start();
            return;
        }

        public void save()
        {
            
        }
    }
}