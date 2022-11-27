using System.Diagnostics;
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

            private Dictionary<string, string> checkChanges()
            {
                //Path, Subtag
                //If a new file. changed[1] will contain a one line parsable manifest on the first line of the file to insert 
                Dictionary<string, string> changed;
                Dictionary<string, string> _new;

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

                ConsoleWrite.writeOutput("Found ", Convert.ToString(changed.Count), " files with changes");

                return changed;
            }

            private Dictionary<string, string> checkChangesFileSystem()
            {
                Dictionary<string, string> changed = new Dictionary<string, string>();
                int ndx = 0;

                ConsoleWrite.writeOutput("Checking local changes..");

                foreach(KeyValuePair<string, ArrayList> xmldb_entries in this.files)
                {
                    if(File.ReadAllText(xmldb_entries.Key) != (string)((ArrayList)(xmldb_entries.Value[0]))[0])
                        changed.Add(xmldb_entries.Key, (string)((ArrayList)(xmldb_entries.Value[0]))[2]);
                    ndx++;
                }

                return changed;
            }

            private bool checkChangesDB()
            {
                //Check wether there have been any changes from any other users in the mean time within the database

                string path, db_content_string, subtag;
                ArrayList content, al_get;
                List<string> changed = new List<string>();
                bool fail = false;
                int ndx = 0;

                ConsoleWrite.writeOutput("Checking remote changes..");

                foreach(KeyValuePair<string, ArrayList> kvp in this.files)
                {
                    path = kvp.Key;
                    content = kvp.Value;
                    subtag = (string)kvp.Value[1];

                    ConsoleWrite.writeOutput("Checking for changes in file: ", path);
                    Scorpion_MDB.ScorpionMicroDB.XMLDBResult result = mdb.doDBSelectiveNoThread(this.database_path, null, this.tag, subtag, mdb.OPCODE_GET);

                    //Console.WriteLine("Subtag: {0} --> {1}", kvp.Key, subtag);
                    
                    al_get = result.getAllDataAsArray();

                    db_content_string = (string)((ArrayList)al_get[0])[0];

                    //DEBUG
                    //ConsoleWrite.writeDebug("Local: ", db_content_string, "\n\n\nXMLDB: ", (string)((ArrayList)(content[0]))[0]);

                    //Fails if the content is not equal to the content in memory
                    if(db_content_string != (string)((ArrayList)(content[0]))[0])
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
                Dictionary<string, string> changed = checkChanges();

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
                    ConsoleWrite.writeOutput("Writing to database..");
                    foreach(KeyValuePair<string, string> path_and_subtag in changed)
                    {
                        ConsoleWrite.writeDebug(path_and_subtag.Value, path_and_subtag.Key);
                        if(!mdb.updateDB(this.database_path, this.tag, path_and_subtag.Value, File.ReadAllText(path_and_subtag.Key)))
                            ConsoleWrite.writeError("Unable to save changes for: ", path_and_subtag.Key);
                        else
                            ConsoleWrite.writeSuccess("Saved changes from ", path_and_subtag.Key, " to XMLDB");
                    }

                    //Save all changes to disk
                    mdb.saveDB(this.database_path, this.password);
                }
                else
                    ConsoleWrite.writeError("Autosave off, or no changes found");

                cleanUp();
                return;
            }

            private void exited(object sender, EventArgs e)
            {
                is_killed = true;
                ConsoleWrite.writeWarning("Exited the chosen editor, changes will be checked locally and remotely. These will be uploaded to the database (Exiting the application until changes are saved may corrupt the database)");
                saveChanges();
                return;
            }
            
            public bool newFile()
            {
                //Adds a new file
                

                return true;
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
                Scorpion_MDB.ScorpionMicroDB.XMLDBResult result = mdb.doDBSelectiveNoThread(this.database_path, null, this.tag, this.subtag, mdb.OPCODE_GET);
                ArrayList al_get = result.getAllDataAsArray();

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
                            this.files.Add($"{this.local_tmp_files_path}/{al_response[1]}.{sub_tag}", new ArrayList() { al_response, sub_tag } );

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
                //Check if the process already exists, if it does, we cannot monitor this process and warn user
                if(Process.GetProcessesByName(this.editor).Length > 0 || Process.GetProcessesByName($"{base_path}/{this.editor}").Length > 0)
                {
                    ScorpionConsoleReadWrite.ConsoleWrite.writeError("Editor already running, scoprion needs to be able to monitor your text editor for when it exits In order to save changes. Please close the current instance or use another editor");
                    return;
                }

                //Do not use the using() statement, as Exited will not fire due to Process's Idisposable nature
                try
                {
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
                }
                catch(Exception e){ ConsoleWrite.writeError(e.Message, ": ", e.StackTrace); }
                return;
            }
        }
    }
}