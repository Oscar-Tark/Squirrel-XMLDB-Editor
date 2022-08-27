using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Scorpion_MDB;
using SquirrelDefaultPaths;
using ScorpionConsoleReadWrite;

namespace XMLDBEditor
{
    public class XMLDBEditorBase
    {
        public XMLDBEditorBase()
        {
            editing_elements = new Dictionary<string, EditingElement>();
            return;
        }

        protected struct EditingElement
        {
            public EditingElement(string database, string tag, string editor = null, string subtag = null)
            {

                //Set elements by default
                this.process = null;
                this.editor = editor;
                this.database = database;
                this.tag = tag;
                this.subtag = subtag;
                this.is_same = true;
                this.is_killed = true;
                mdb = new ScorpionMicroDB();

                return;
            }

            Process process;
            string editor;
            string database;
            string tag;
            string subtag;
            bool is_same;
            bool is_killed;
            ScorpionMicroDB mdb;

            private void exited( object sender, EventArgs e)
            {
                if(process.HasExited)
                {
                    is_killed = true;
                    saveChanges();
                }
            }

            public void start()
            {
                string base_path = "";

                //Check if the editor is available in /bin or /usr/bin else: return
                ConsoleWrite.writeWarning("Any editor must be installed to /bin or /usr/bin");
                if(File.Exists($"{LinuxSystemPaths.bin_path}/{this.editor}"))
                    base_path = LinuxSystemPaths.bin_path;
                else if(File.Exists($"{LinuxSystemPaths.usr_bin_path}/{this.editor}"))
                    base_path = LinuxSystemPaths.usr_bin_path;
                else
                {
                    ConsoleWrite.writeError($"Specified editor {this.editor} not found. Editing request stopped");
                    return;
                }
                
                //Process for the editor
                using(this.process = new Process())
                {
                    this.process.StartInfo.FileName = $"{base_path}/{this.editor}";
                    this.process.EnableRaisingEvents = true;
                    process.Exited += new EventHandler(exited);
                }
            }

            public void saveChanges()
            {
                
            }
        }

        protected Dictionary<string, EditingElement> editing_elements; 
    }
}