using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Scorpion_MDB;
using SquirrelDefaultPaths;
using ScorpionConsoleReadWrite;
using System.Collections;

namespace XMLDBEditor
{
    public class XMLDBEditorBase
    {
        public XMLDBEditorBase()
        {
            editing_elements = new Dictionary<string, EditingElement>();
            return;
        }

        protected Dictionary<string, EditingElement> editing_elements; 

        protected struct EditingElement
        {
            Process process;
            string editor;

            ScorpionMicroDB mdb;
            string password;
            string database_path;
            string database_path_neutral;
            string tag;
            string subtag;

            string local_tmp_files_path;

            bool autosave;
            bool is_same;
            bool is_killed;

            List<string> open_files;
            Dictionary<string, ArrayList> files;

            public EditingElement(string database_path, string tag, string password, string editor = null, string subtag = null, bool autosave = true)
            {

                //Set elements by default
                this.process = null;
                this.editor = editor;
                this.database_path = database_path;
                this.tag = tag;
                this.subtag = subtag;
                this.autosave = autosave;
                this.is_same = true;
                this.is_killed = true;
                this.password = password;
                this.database_path_neutral = database_path.Replace("/", "_");
                this.local_tmp_files_path = $"{SquirrelDefaultPaths.SquirrelPaths.main_tmp_path}/{this.database_path_neutral}";

                //Start any instance members
                mdb = new ScorpionMicroDB();
                open_files = new List<string>();
                files = new Dictionary<string, ArrayList>();

                return;
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

                //Extract files to /tmp/squirrelees/
                this.open_files = getFromDB();
                
                //Process for the editor
                startEditor(ref base_path, open_files);

                return;
            }

            //Remove all temporary files without saving anything to the database and close the database connection
            public void cleanUp()
            {
                //If the editor process has not exited, kill it
                if(!this.process.HasExited)
                    this.process.Kill();
                
                //Delete all temporary files
                foreach(string file in this.open_files)
                {
                    ConsoleWrite.writeOutput("Deleting temporary file: ", file);
                    if(File.Exists(file))
                        File.Delete(file);
                }
                Directory.Delete(this.local_tmp_files_path);

                mdb.closeDB(this.database_path);
                return;
            }

            private List<string> checkChanges()
            {
                List<string> changed;

                //Check if the content has changed from the DB and local version, if not reload the db version, try three times
                for(int i = 0; i < 3; i++)
                {
                    if(checkChangesDB())
                    {
                        ConsoleWrite.writeError("The content in local memory and in the XMLDB database are different. Was the database updated by someone else before you tried saving changes?");
                        return null;
                    }
                    else
                        break;
                }
                
                //Check if files in /tmp and locally contained files
                changed = checkChangesFileSystem();
                return changed;
            }

            private List<string> checkChangesFileSystem()
            {
                List<string> changed = new List<string>();
                int ndx = 0;

                foreach(KeyValuePair<string, ArrayList> xmldb_entries in this.files)
                {
                    if(File.ReadAllText(xmldb_entries.Key) != xmldb_entries.Value[0])
                        changed.Add(xmldb_entries.Key);
                    ndx++;
                }

                return changed;
            }

            private bool checkChangesDB()
            {
                string path, db_content_string;
                ArrayList content, al_get;
                List<string> changed = new List<string>();
                bool fail = false;
                int ndx = 0;

                foreach(KeyValuePair<string, ArrayList> kvp in this.files)
                {
                    path = kvp.Key;
                    content = kvp.Value;

                    ConsoleWrite.writeOutput("Checking for changes in file: ", path);
                    al_get = mdb.doDBSelectiveNoThread(this.database_path, null, this.tag, this.subtag, mdb.OPCODE_GET);

                    db_content_string = (string)((ArrayList)al_get[0])[0];

                    //DEBUG
                    ConsoleWrite.writeDebug(db_content_string, "\n\n\n", (string)content[0]);

                    //Fails if the content is not equal to the content in memory
                    if(db_content_string != content[0])
                    {
                        fail = true;
                        break;
                    }
                    ndx++;
                }
                return fail;
            }

            //Save all changes to the database and then cleanUp();
            public void saveChanges()
            {
                List <string> changed = checkChanges();

                //If checks failed then return...
                if(changed == null)
                {
                    ConsoleWrite.writeError("Unable to save changes. Cleanup local files? (Y/N)");
                    if(ConsoleRead.ReadInput().ToLower() == "y")
                        cleanUp();
                    return;
                }

                //Save all changes to file only if autosave is on
                if(this.autosave && changed.Count > 0)
                {
                    foreach(string s in changed)
                    {
                        
                    }
                }
                else
                    ConsoleWrite.writeError("Autosave off, or no changes found");

                cleanUp();
                return;
            }

            private void exited(object sender, EventArgs e)
            {
                is_killed = true;
                ConsoleWrite.writeWarning("Exited the chosen editor, changes will be uploaded to the database (Exiting the application until changes are saved may corrupt the database)");
                saveChanges();
                return;
            }

            private List<string> getFromDB()
            {
                List<string> files_written = new List<string>();
                mdb = new ScorpionMicroDB();
                
                if(!Directory.Exists(SquirrelDefaultPaths.SquirrelPaths.main_tmp_path))
                    Directory.CreateDirectory(SquirrelDefaultPaths.SquirrelPaths.main_tmp_path);

                if(!Directory.Exists(this.local_tmp_files_path))
                    Directory.CreateDirectory(this.local_tmp_files_path);

                mdb.loadDB(this.database_path, this.database_path, this.password);
                ArrayList al_get = mdb.doDBSelectiveNoThread(this.database_path, null, this.tag, this.subtag, mdb.OPCODE_GET);

                ConsoleWrite.writeSpecial("Got ", al_get.Count.ToString(), " results");

                string sub_tag;
                try
                {
                    foreach(ArrayList al_response in al_get)
                    {
                        sub_tag = (string)(al_response[2] == null ? "" : al_response[2]);
                        using(FileStream fs = File.Create($"{this.local_tmp_files_path}/{al_response[1]}.{sub_tag}"))
                        {
                            ConsoleWrite.writeOutput($"Writing: {this.local_tmp_files_path}/{al_response[1]}.{sub_tag}");
                            StreamWriter sw = new StreamWriter(fs);
                            sw.Write((string)al_response[0]);
                            sw.Flush();
                            fs.Flush();
                            sw.Close();
                            fs.Close();
                            ConsoleWrite.writeSuccess($"Wrote: {this.local_tmp_files_path}/{al_response[1]}.{sub_tag}");

                            //Add actual contents and path
                            this.files.Add($"{this.local_tmp_files_path}/{al_response[1]}.{sub_tag}", al_response);

                            //Add paths
                            files_written.Add($"{this.local_tmp_files_path}/{al_response[1]}.{sub_tag}");
                        }
                    }
                }
                catch(Exception e ){ ConsoleWrite.writeError(e.Message, e.StackTrace); }
                return files_written;
            }

            private void startEditor(ref string base_path, List<string> arguments)
            {
                //Do not use the using() statement, as Exited will not fire due to Process's Idisposable nature
                this.process = new Process();
                this.process.StartInfo.FileName = $"{base_path}/{this.editor}";
                this.process.EnableRaisingEvents = true;
                this.process.StartInfo.Arguments = string.Join( " ", arguments);
                process.Exited += new EventHandler(exited);
                process.Start();
                this.open_files = arguments;

                if(!process.HasExited)
                    this.is_killed = false;
                else
                    cleanUp();
                return;
            }
        }
    }
}