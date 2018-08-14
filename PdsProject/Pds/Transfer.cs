using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pds
{
    class Transfer //si occupa della ricezione del file
    {
        TcpClient client;
        App application;
        private DateTime updateTime;

        public Transfer(App app, TcpClient cl)
        {
            application = app;
            client = cl;
            fn_prbar_();
        }


        public async void fn_prbar_()
        {
            Int32 BufferSize = 1024;
            byte[] isDir = new byte[1];
            byte[] tmpSname_B;
            byte[] tmpSnameLength_B = new byte[4]; //32 bit
            byte[] fileName_B;
            byte[] fileNameLength_B = new byte[4]; //32 bit
            byte[] fileLength_B = new byte[8];//64 bit
            byte[] userName_B;
            byte[] userNameLength_B = new byte[4]; //32 bit
            byte[] RecData = new byte[BufferSize];
            int RecBytes;
            string SaveFileName1 = null;
            FileStream Fs = null;

            try
            {
                //---get the incoming data through a network stream---
                NetworkStream nwStream = client.GetStream();
                nwStream.ReadTimeout = 15000;
                nwStream.WriteTimeout = 15000;

                byte[] buffer = new byte[client.ReceiveBufferSize];
                nwStream.Read(isDir, 0, 1); //ricevo l'informazione se si tratta di una cartella o un file
                //---read incoming stream---
                int bytesRead = await nwStream.ReadAsync(tmpSnameLength_B, 0, 4); //leggo la lunghezza del nome di tempTOSENDxxxxx
                bytesRead = await nwStream.ReadAsync(fileNameLength_B, 0, 4);
                bytesRead = await nwStream.ReadAsync(fileLength_B, 0, 8);
                bytesRead = await nwStream.ReadAsync(userNameLength_B, 0, 4);

                tmpSname_B = new byte[BitConverter.ToInt32(tmpSnameLength_B, 0)];//dopo che ho ricevuto tutte le lunghezze, posso allocare lo spazio necessario a memorizzare i nomi
                fileName_B = new byte[BitConverter.ToInt32(fileNameLength_B, 0)];
                userName_B = new byte[BitConverter.ToInt32(userNameLength_B, 0)];

                bytesRead = await nwStream.ReadAsync(tmpSname_B, 0, BitConverter.ToInt32(tmpSnameLength_B, 0)); //salva il nome della tempTOSENDxxxxx mandata
                bytesRead = await nwStream.ReadAsync(fileName_B, 0, BitConverter.ToInt32(fileNameLength_B, 0));
                bytesRead = await nwStream.ReadAsync(userName_B, 0, BitConverter.ToInt32(userNameLength_B, 0));


                if (Pds.Properties.Settings.Default["AutoActivated"].Equals("NO"))
                { //se aspetto la risposta del destinatario prima di inviare
                    if (System.Windows.Forms.MessageBox.Show(String.Format("{0} vorrebbe inviarti qualcosa: \r\n\r\n   {1} \r\n ", ASCIIEncoding.ASCII.GetString(userName_B), ASCIIEncoding.ASCII.GetString(fileName_B)), "", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    { //se il ricevente ha risposto no alla domanda: vuoi ricevere il file dall'utente y?
                         nwStream.WriteByte(0);//canSend di Form2 + 0
                        return;
                    }
                }
                 nwStream.WriteByte(1);//canSend di Form2 =1


                int totalrecbytes = 0;
                string nomeFile = ASCIIEncoding.ASCII.GetString(fileName_B);

                string SaveFileName = null;
                string PathToSave = null;

                if (application.default_path == true)
                {
                    PathToSave = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);//il nostro path di salvataggio di default é il desktop
                    SaveFileName1 = "./tempTORECEIVE";
                    SaveFileName1 = String.Concat(SaveFileName1 , DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                    string PathRecInc = SaveFileName1;

                    int i = 1;
                    while (Directory.Exists(SaveFileName1))//nel caso ricevo due cartelle simultaneamente (e quindi ho due tempTORECEIVE con lo stessso nome)
                    {
                        SaveFileName1 = PathRecInc + i.ToString(); //aggiungo un indice incrementale
                        i++;
                    }
                    Directory.CreateDirectory(SaveFileName1);

                }
                else
                {   //se nelle impostazioni é stata configurata l'opzione di scegliere il percorso di salvataggio alla ricezione
                    FolderBrowserDialog folder = new FolderBrowserDialog();
                    folder.Description = "Seleziona la cartella in cui salvare il file";
                    if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        PathToSave = folder.SelectedPath.ToString();
                    }

                    SaveFileName1 = "./tempTORECEIVE";
                    SaveFileName1 = SaveFileName1 + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    string PathRecInc = SaveFileName1;
                    int i = 1;
                    while (Directory.Exists(SaveFileName1))
                    {
                        SaveFileName1 = PathRecInc + i.ToString();
                        i++;
                    }
                    Directory.CreateDirectory(SaveFileName1);

                }


                SaveFileName = System.IO.Path.Combine(SaveFileName1, Encoding.ASCII.GetString(tmpSname_B)+".zip");
                //SaveFileName = ./tempTORECEIVExxxx/tempTOSENDxxxxxx.zip

               Fs= new FileStream(SaveFileName, FileMode.OpenOrCreate, FileAccess.Write);

                DateTime startTime = DateTime.Now;
                updateTime = DateTime.Now;
                var at_r = Task.Run(() => CheckConnect()); //se l'host che sta inviando ci mette più di 30sec a inviarmi un buffer di dati lo ritengo disconnesso e smetto di aspettare in ricezione
                while ((RecBytes =await nwStream.ReadAsync(RecData, 0, RecData.Length)) > 0)
                {
                    updateTime = DateTime.Now;
                    TimeSpan passedTime = (DateTime.Now - startTime);
                    totalrecbytes += RecBytes;
                    Fs.Write(RecData, 0, RecBytes);

                }

                if (totalrecbytes != BitConverter.ToInt64(fileLength_B, 0))//controllo che il n di byte che ho effettivamente ricevuto é quello che mi aspettavo di ricevere
                {
                    Fs.Close();
                    client.Close();
                    try
                    {
                        Directory.Delete(SaveFileName1, true);
                    }
                    catch (IOException ie)
                    {
                        Directory.Delete(SaveFileName1, true);
                    }
                    catch (UnauthorizedAccessException ae)
                    {
                        Directory.Delete(SaveFileName1, true);
                    }
                    Thread.Sleep(200);
                    Window1.ShowMessageBox("Ricezione file interrotta!");

                    return;
                }
                Fs.Close();
                client.Close();
                if (isDir[0] == 1)
                {
                  ExtractZip(SaveFileName1, SaveFileName, PathToSave, ASCIIEncoding.ASCII.GetString(fileName_B), true, ASCIIEncoding.ASCII.GetString( tmpSname_B));
                }
                else
                {
                  ExtractZip(SaveFileName1, SaveFileName, PathToSave, ASCIIEncoding.ASCII.GetString(fileName_B), false, ASCIIEncoding.ASCII.GetString( tmpSname_B));

                }
                Window1.ShowMessageBox("File ricevuto con successo!");


            }
            catch (Exception ex)
            {
                Console.WriteLine("Errore su ricezione dati : " + ex.Message );
                Window1.ShowMessageBox("Ricezione file interrotta!");
                if(Fs!=null)
                Fs.Close();

                client.Close();

                if(SaveFileName1!=null)
                Directory.Delete(SaveFileName1, true);


            }

        }

        //ricevo un tempTOSEND.zip -> estraggo in tempTORECEIVE -> rinomino se serve -> copio nel Path in cui voglio salvare -> elimino tempTORECEIVExxxxx
        public int ExtractZip(String remPath, String path, string Pathtosave, string filename, bool isdir, string tmpSname)
        {
            try
            {
                //remPath=tempTORECEIVExxxx
                //path = tempTORECEIVExxxx/filename
                string FullPath = Path.GetFullPath(path);
                string InitialPath = FullPath.Remove(FullPath.Length - (Path.GetFileName(FullPath)).Length);
                ZipFile.ExtractToDirectory(path, InitialPath); //estraggo tempTOSENDxxxx.zip in tempTORECEIVExxxx
                File.Delete(path); // elimino tempTOSENDxxx.zip
                string finalPath = null;
                string fromPath = null;

                if (isdir == true)
                {
                    if (Directory.Exists(Path.Combine(Pathtosave, filename)))//Pathtosave: directory di destinazione del file/cartella ricevuta
                    {// se il direttorio esisteva già con quel nome nel percorso di destinazione aggiungo un count
                        string temp = filename;
                        for (int cnt = 0; ;)
                        {
                            var fileCount = (from file in Directory.EnumerateFiles(Pathtosave, Path.GetFileName(temp), SearchOption.TopDirectoryOnly) select file).Count();//conto il numero di file in PathToSave che hanno lo stesso nome di quella che ho appena ricevuto
                            temp = Pathtosave + "\\" + Path.GetFileNameWithoutExtension(filename) + "(" + (cnt + fileCount + 1) + ")" + Path.GetExtension(filename);//ci aggiungo 1
                            if (!Directory.Exists(temp)) //mi fermo quando ho trovato un nome che non esiste nel percorso di destinazione
                            {
                                finalPath = temp;
                                break;
                            }
                            cnt++;
                        }
                    }
                    else
                    {
                        finalPath = Path.Combine(Pathtosave, filename);
                    }

                    //finalPath= nome del file o della cartella rinominato se già esisteva
                    filename = Path.Combine(tmpSname, filename);
                    fromPath = Path.Combine(InitialPath, filename);
                    Directory.Move(fromPath, finalPath);
                    Directory.Delete(remPath, true);
                }
                else
                {
                    if (File.Exists(Path.Combine(Pathtosave, filename)))
                    {
                        string temp = filename;
                        for (int cnt = 0; ;)
                        {
                            var fileCount = (from file in Directory.EnumerateFiles(Pathtosave, Path.GetFileName(temp), SearchOption.TopDirectoryOnly) select file).Count();
                            temp = Pathtosave + "\\" + Path.GetFileNameWithoutExtension(filename) + "(" + (cnt + fileCount + 1) + ")" + Path.GetExtension(filename);
                            if (!File.Exists(temp))
                            {
                                finalPath = temp;
                                break;
                            }
                            cnt++;
                        }
                    }
                    else
                    {
                        finalPath = Path.Combine(Pathtosave, filename);
                    }

                    //finalPath= nome del file o della cartella rinominato se già esisteva
                    filename = Path.Combine(tmpSname, filename); //tempTOSEND/filename
                    fromPath = Path.Combine(InitialPath, filename);//tempTORECEIVE//tempTOSENDxxx/filename
                    Directory.Move(fromPath, finalPath); //sposto il file nel finalPath
                    Directory.Delete(remPath, true); //cancello tempTORECEIVExxxx
                }
            }
            catch (Exception)
            {
                Window1.ShowMessageBox("Impossibile decomprimere il file ricevuto!");
                throw;
            }

            return 1;

        }

        //funzione che interrompe il trasferiemnto del file se non si riceve niente per più di 30 secondi
        private void CheckConnect()
        {
            bool finish = false;
            while (!finish)
            {
                TimeSpan diff = DateTime.Now - updateTime;
                TimeSpan baseInterval = new TimeSpan(0, 0, 30);
                if (TimeSpan.Compare(diff, baseInterval) == 1)
                {
                    finish = true;
                    client.Close();
                }
            }
        }
    }
}
