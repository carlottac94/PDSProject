using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO.Compression;
using System.Windows.Forms;
using System.Security.Permissions;
using System.Security.AccessControl;
using System.Threading.Tasks;

namespace Pds
{
    public partial class Form2 : Form
    {
        bool isDir = false;
        private  int annulla;
        private DateTime updateTime;
        TcpClient client = new TcpClient();
        private bool finish = false;

        public Form2(IPEndPoint ipDest, String path)
        {
            InitializeComponent();
            Show();
            SendData(ipDest,path);
        }


        public async void SendData(IPEndPoint ipDest, string path)
        {
            string Path = null;
            byte[] SendingBuffer = null;
            FileInfo fileinfo = null;
            FileStream filestream = null;
           
            try
            {
                client.Connect(ipDest);

            }
            catch (Exception e)
            {

                System.Windows.MessageBox.Show("Errore connessione con host. \nDettagli: " + e);

            }
            try
            {
                FileAttributes attr = File.GetAttributes(path);

            //verifico se si tratta di file o di direttorio
            if (attr.HasFlag(FileAttributes.Directory))
            {
                isDir = true;
            }
            else
            {
                isDir = false;
              
            }

           
                Char delimiter = '.';
            String[] substrings = ipDest.Address.ToString().Split(delimiter);
            Path = CreateZip(path, substrings[3]); //passo alla funzione CreateZip l'utlima cifra dell'indirizzo ip del destinatario
            //Path ora contiene il nome della cartella zippata
            fileinfo = new FileInfo(Path);
            filestream = fileinfo.OpenRead();

           
                Text = "Invio in corso...   ";
                NetworkStream nwStream = client.GetStream();
                nwStream.WriteTimeout = 15000;
                nwStream.ReadTimeout = 15000;

                string strtmp = System.IO.Path.GetFileName(Path); 
                string strtmp2 = strtmp.Substring(0,strtmp.Length - 4); //-4 per togliere ".zip"
                byte[]tmpSname = ASCIIEncoding.ASCII.GetBytes(strtmp2);//mando anche il nome della directory tempTOSENDxxxxxx
                byte[] tmpSnameLenght = BitConverter.GetBytes(strtmp2.Length);
                byte[] fileName = ASCIIEncoding.ASCII.GetBytes(System.IO.Path.GetFileName(path));
                byte[] fileNameLength = BitConverter.GetBytes(fileName.Length); //32 bit
                byte[] fileLength = BitConverter.GetBytes(filestream.Length);//64 bit
                byte[] userName = ASCIIEncoding.ASCII.GetBytes(Environment.UserName);
                byte[] userNameLength = BitConverter.GetBytes(userName.Length); //32 bit
                byte[] canSend = new byte[1];


                //---send the text---
                if (isDir == true) //invio un byte a 1 se invio una cartella, un byte a 0 se invio un file
                    nwStream.WriteByte(1);
                else
                    nwStream.WriteByte(0);
                await nwStream.WriteAsync(tmpSnameLenght, 0, tmpSnameLenght.Length);
                await nwStream.WriteAsync(fileNameLength, 0, fileNameLength.Length);
                await nwStream.WriteAsync(fileLength, 0, fileLength.Length);
                await nwStream.WriteAsync(userNameLength, 0, userNameLength.Length);
                await nwStream.WriteAsync(tmpSname, 0, tmpSname.Length);
                await nwStream.WriteAsync(fileName, 0, fileName.Length);
                await nwStream.WriteAsync(userName, 0, userName.Length);
                updateTime = DateTime.Now;
                var at_r = Task.Run(() => CheckConnect());
                await nwStream.ReadAsync(canSend, 0, 1);
                finish = true;

                Int32 BufferSize = 1024;
                int NoOfPackets = Convert.ToInt32
                      (Math.Ceiling(Convert.ToDouble(filestream.Length) / Convert.ToDouble(BufferSize)));
                //calcolo il numero di pacchetti che devo inviare, avendo un buffer definito di 1024 byte
                if (canSend[0] == 0) //byte che indica la risposta al popup: "vuoi ricevere questo file da x?", se la ricezione automatica non è attiva 
                {
                    Window1.ShowMessageBox("Invio file interrotto!");
                    return;
                }


                progressBar1.Value = 0;
                int TotalLength = (int)filestream.Length, CurrentPacketLength;
                int sent = 0;
                DateTime startTime = DateTime.Now;
                annulla = 0;

                for (int i = 0; i < NoOfPackets; i++)
                {

                    if (annulla == 1) //flag annulla settato a 1 quando l'utente che invia clicca su annulla
                    {
                        throw new Exception("Invio del file interrotto.");
                        
                    }

                    TimeSpan passedTime = (DateTime.Now - startTime);
                    
                    if (TotalLength > BufferSize)
                    {
                        CurrentPacketLength = BufferSize;
                        TotalLength = TotalLength - CurrentPacketLength;
                    }
                    else
                        CurrentPacketLength = TotalLength;
                    
                    SendingBuffer = new byte[CurrentPacketLength];
                   await filestream.ReadAsync(SendingBuffer, 0, CurrentPacketLength); //leggo dal file
                    Console.WriteLine("Bloccato da filestream.Read");
                    await nwStream.WriteAsync(SendingBuffer, 0, (int)SendingBuffer.Length); //mando i file letti dal file via tcp
                    Console.WriteLine("Bloccato da nwStream.writeAsync");

                    //Console.WriteLine("perc sent: " + sent);
                    sent += CurrentPacketLength;
                    progressBar1.Value = (int)((100d * sent) / fileinfo.Length); //calcolo la percentuale di file mandata per far avanzare la barra
                    
                    
                    label1.Text = "Invio in corso...  " + progressBar1.Value.ToString() + "%";
                    //proporzione tempo_rimanente : tempo_passato = valorebarra_corrente : valorebarra_rimanente 
                    int Remainingsecs = (int)(passedTime.TotalSeconds / (progressBar1.Value + 1) * (progressBar1.Maximum - progressBar1.Value));
                    int Remainingmins = Remainingsecs / 60;

                    if (Remainingmins == 0)
                    {
                        label2.Text = "Tempo rimanente: " + Remainingsecs.ToString() + " secondi ";
                    }
                    else
                    {
                        label2.Text = "Tempo rimanente: " + Remainingmins.ToString() + " minuti e " + (Remainingsecs % 60).ToString() + " secondi ";
                    }

                }

                if (annulla == 0)
                    Window1.ShowMessageBox("File inviato con successo!");
                else
                    Window1.ShowMessageBox("Invio file interrotto!");


            }
            catch (Exception ex)
            {
                if(ex is InvalidOperationException) //eccezione sollevata dalla read e dalla write async
                Console.WriteLine("Errore trasferimento dati:" + ex.Message);
                Window1.ShowMessageBox("Invio file interrotto!");
            }
            finally
            {
                if(filestream!=null)
                filestream.Close();
                if(Path!=null)
                File.Delete(Path);
                client.Close();
                Hide();
            }

        }
        
       private string CreateZip(string pathdir,string ip) // mette tutto in "tempTOSEND" e lo zippa per inviare 
        {
            //pathdir = path del file o cartella che voglio inviare
            //ip = ultima cifra dell'ip che distingue l'host nella rete locale
            string newPathDir = "./tempTOSEND";//add
            newPathDir = newPathDir+ip;
            newPathDir= String.Concat(newPathDir, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            //il nome della cartella temporanea sarà tempTOSEND+ultima cifra ip+ tempo di creazione 
            //in questo modo se lo stesso host manda due file le due temptosend si distingueranno dal tempo di creazione
            //se due host diversi mandano due file simultaneamente, le due cartelle si potranno distinguere dall'ip dell'host
            //la cartella temporanea serve per avere il tempo di controllare di non avere due cartelle o file con lo stesso nome nel path di destinazione
            string dir = null;
            string newPathDir3 = null;
            try
            {
               dir = Directory.CreateDirectory(newPathDir).FullName;
                //dir= path completo della directory che conterrà l'oggetto da inviare (temptosend)

                string currdir;
                if (isDir == true)
                {   //metto la mia directory all'interno della temptosend
                    currdir = Path.Combine(dir, Path.GetFileName(pathdir)); 
                    Directory.CreateDirectory(currdir);
                    CloneDirectory(pathdir, currdir);
                }
                else
                {   //metto il mio file all'interno della temptosend
                    currdir = Path.Combine(dir, Path.GetFileName(pathdir));
                    File.Copy(pathdir, currdir);
                }

                //devo zippare DIR per inviarla (ora contiene esattamente l'oggeto che voglio mandare)
                string dirname = Path.GetFileName(dir);
                string newPathDir2 = dirname + ".zip";
                string FullPath = Path.GetFullPath(dir);
                string InitialPath = FullPath.Remove(FullPath.Length - (Path.GetFileName(dir)).Length); //percorso dove si trova il file che voglio mandare
                 newPathDir3 = Path.Combine(InitialPath, newPathDir2); //nome dello zip che vuoi creare, in modo che venga creato esattamente nel percorso dove si trova il file originale
                //newPAthDir3= path della directory zippata

                ZipFile.CreateFromDirectory(dir, newPathDir3, CompressionLevel.NoCompression, true);
            }
            catch (Exception)
            {
                throw;
            }
            finally {
                //elimino la directory provvisoria tempTOSEND... non zippata
                if(dir!=null)
                Directory.Delete(dir, true);
            }
            return newPathDir3;


        }
        private static void CloneDirectory(string root, string dest)
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);
                if (!Directory.Exists(Path.Combine(dest, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(dest, dirName));
                }
                CloneDirectory(directory, Path.Combine(dest, dirName));
            }

            foreach (var file in Directory.GetFiles(root))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            }
        }

        private void button1_Click(object sender, EventArgs e) //event handler del bottone annulla
        {
            annulla = 1;
        }
        private void CheckConnect() //funzione che controlla che la readasync che legge canSend non ci metta più di 60 sec a tornare; altrimenti assumo l'host disconnesso e vado avanti 
        {
            while (!finish)
            {
                TimeSpan diff = DateTime.Now - updateTime;
                TimeSpan baseInterval = new TimeSpan(0, 0, 60);
                if (TimeSpan.Compare(diff, baseInterval) == 1)
                {
                    finish = true;
                    client.Close();
                }
            }
        }
    }
}


