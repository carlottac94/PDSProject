using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Reflection;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using System.IO.Compression;
using Pds.Properties;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.IO.Pipes;

namespace Pds
{
    /// <summary>
    /// Logica di interazione per App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private RegistryKey key_file;
        private RegistryKey key_cartella;
        private string myIP;
        private string hostname;
        private List<Task> TaskList = new List<Task>();//lista delle operazioni asincrone che i 4 thread creati da SetEnvironnment dovranno eseguire
        private CancellationTokenSource cts = new CancellationTokenSource();//ogg. proprietario del token di cancellazione 
        private List<User> UserList = new List<User>();
        private ListaUtenti ListaUtentiWLiindow;//= new ListaUtenti();
        public Dispatcher dispatcher;
        public ReaderWriterLockSlim lock_disp;
        private UdpClient server;
        public bool default_image = true;
        public bool default_path = true;
        private int PORT= 15000;
        private int PORT_REC = 17000;
        BitmapSource bitmapSourcePicture = null;
        public bool visible = true; //visible true=> stato online, gli altri utenti in lan mi vedono
        private List<string> listArgs = new List<string>();


        void Main(object sender, StartupEventArgs args) //evento gestito dal main: click su "condividi con"
        {
 
            if (args.Args.Length > 0)   // non essendo il server principale(corrisponde a condividi con ),prendo il nome del file o cartella e muore
            {   //se l'utente ha cliccato su condividi con passo il nome del file 
                int i = 0;

                foreach (string s in Environment.GetCommandLineArgs())
                {
                    System.Console.WriteLine("--" + s);

                    if (i > 0) //salto il primo argomento
                    {
                        listArgs.Add(s); //lista che contiene i nomi dei file da inviare
                    }
                    i++;
                }

                SendPath(listArgs[0]);

                Current.Shutdown();
                return;
            }
            else
            {
                SetEnvironment();
                Window1 win1 = new Window1(this); //creo la finestra delle impostazioni di condivisione


            }
        }

      
        private void SetEnvironment()
        {   // funzione che inizializza tutti i parametri dell'environnment e lancia i thread

            //creo l'opzione condividi con nel menu contestuale (*->per tutti i file)
            key_file = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con"); //questa key entry è una chiave
            key_file = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con\command"); //questa key entry è una stringa
            key_file.SetValue("", Assembly.GetExecutingAssembly().Location + " \"%1\"");  //setto il valore della seconda key entry alla location dell'assembly in cui si trova il codice + il path del file su cui ho chiamato il menu context (che rimpiazzerà il %1)

            //creo l'opzione condividi con nel menu contestuale (per le cartelle)
            key_cartella = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con");
            key_cartella = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con\command");
            key_cartella.SetValue("", Assembly.GetExecutingAssembly().Location + " \"%1\"");

            dispatcher = Dispatcher.CurrentDispatcher;
            lock_disp = new ReaderWriterLockSlim(); //lock che protegge una risorsa che può essere letta da più thread contemporaneamente e scritta da uno solo alla volta

            Pds.Properties.Settings.Default["Status"] = "Online";
            Pds.Properties.Settings.Default["AutoActivated"] = "NO"; //se autoactivated=NO, una finestra di richiesta appare prima della ricezione del file 
            TaskList.Add(Task.Factory.StartNew(() => AnnunciaPresenza(), cts.Token)); //argomenti: azione, token di cancellazione --- thread che annuncia la presena agli altri utenti
            TaskList.Add(Task.Factory.StartNew(() => ScopriPresenza(), cts.Token));  //thread che crea una listautenti in base ai pacchetti ricevuti dagli altri eventuali utenti in lan
            TaskList.Add(Task.Factory.StartNew(() => ReceivePath(), cts.Token));  //thread che si occupa della ricezione del path del file/cartella
            TaskList.Add(Task.Factory.StartNew(() => ReceiveData(), cts.Token)); //thread che si occupa della ricezione del contenuto stesso del file


        }


        ~App()
        {

            if (Environment.GetCommandLineArgs().Length == 1)
            {
                visible = false;
                //  cancello la chiave di registro (ContextMenu)
                key_file = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con\command");
                if (key_file != null)
                {
                    key_file.Close();
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con\command");
                }
                key_cartella = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con\command");
                if (key_cartella != null)
                {
                    key_cartella.Close();
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con\command");
                }
                key_file = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con");

                if (key_file != null)
                {
                    key_file.Close();
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\CLASSES\*\shell\Condividi con");
                }
                key_cartella = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con");
                if (key_cartella != null)
                {
                    key_cartella.Close();
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\CLASSES\Folder\shell\Condividi con");
                }

                try
                {
                    cts.Cancel();  //la notifica di cancellazione viene inviata tramite il token a tutti i tasks, che così sanno che devono terminare

                    Task.WaitAll(TaskList.ToArray());  //Attende il completamento dell'esecuzione di tutti gli oggetti Task forniti.
                }
                catch (Exception e)
                {

                    Console.WriteLine("Errore : " + e);
                }
                finally
                {
                    cts.Dispose(); //elimina il cts
                }
                
            }


        }
        
        private void AnnunciaPresenza()
        {   //per annunciare la presenza usiamo udp in broadcast
            //un host annuncia la propria presenza mandando i parametri del profilo: usernme, la sua foto profilo e il suo indirizzo ip

            UdpClient client = new UdpClient();
            Socket socket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            socket.EnableBroadcast = true;
           
            while (!cts.Token.IsCancellationRequested)
            {

                   hostname = Dns.GetHostName().ToString();
                   IPAddress[] IPv4Addresses = Array.FindAll(Dns.GetHostEntry(hostname).AddressList,
                                                                  a => a.AddressFamily == AddressFamily.InterNetwork);

                   myIP = IPv4Addresses[0].ToString();
                try
                {
                    
                        String pathImage = null;
                        if (default_image == true)
                        {
                            pathImage = "../../default_user_image.jpg";
                            bitmapSourcePicture = GetImageUser(pathImage);
                         }
                    else //se l'utente ha scelto tramite le impostazioni un'immagine diversa da quella di default
                    {
                            pathImage = Pds.Properties.Settings.Default["ImagePath"].ToString();// PathImageNODefault; le impostazioni di condivisione settano il path dell'immagine dell'utente
                            bitmapSourcePicture = GetImageUser(pathImage);
                        }

                   
                        User user = new User
                        {
                            Name = Environment.UserName,
                            IPAddress = myIP,
                            Image = bitmapSourcePicture
                        };
                            string dati = null;

                            if (visible == false)
                                dati = "0-" + user.Name + "-" + user.IPAddress + "-" + BitmapToBase64(GetBitmap(user.Image));
                            else
                                dati = "1-" + user.Name + "-" + user.IPAddress + "-" + BitmapToBase64(GetBitmap(user.Image));
                      
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), PORT);
                    //gestione del caso in cui l'host perde la conessione
                    if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    {
                        Window1.ShowMessageBox("Sei disconnesso!");
                    }
                    while(!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) ;

                        client.Send(Encoding.ASCII.GetBytes(dati), Encoding.ASCII.GetBytes(dati).Length, endPoint);
                }



                catch (Exception e)
                {
                    System.Windows.MessageBox.Show("Non è stato possibile connettersi in LAN.\n Dettagli: " + e);

                    dispatcher.Invoke(() => //lo shutdown non può essere fatto da un thread non UI-> ho bisogno del dispatcher!
                    {
                        lock_disp.EnterWriteLock();
                        try
                        {
                            Shutdown();
                        }
                        finally
                        {
                            lock_disp.ExitWriteLock();
                        }
                    }, DispatcherPriority.DataBind);
                }
                Thread.Sleep(1500); //viene inviato un pacchetto per annunciare la presenza ogni 15 sec

            }
          
            client.Close();
        }
        private void ScopriPresenza()
        {

             server = new UdpClient(PORT);
            while (!cts.Token.IsCancellationRequested)
            {
                User user = new User();
                char OffUser='0'; //se ricevo '0'-> ho trovato un host offline, '1' -> host online
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);

                    byte[] datiRicevuti = server.Receive(ref endPoint);
                    string dati = Encoding.ASCII.GetString(datiRicevuti);

                    Char delimiter = '-';
                    String[] substrings = dati.Split(delimiter);
                    OffUser = substrings[0][0] ;
                    user.Name = substrings[1];
                    user.IPAddress = substrings[2];
                    user.time = DateTime.Now;
                    BitmapSource bitmapSource = BitmapToBitmapSource(Base64ToBitmap(substrings[3]));
                    user.Image = bitmapSource;
                    bitmapSource.Freeze(); //visibile  tutti i thread; oggetto non modificabile

                    System.Console.WriteLine("ricevuto : " + user.Name + " " + user.IPAddress +" " + OffUser.ToString() );
                }
                
                    catch (Exception e)
                {
                    System.Windows.MessageBox.Show("Non è stato possibile connettersi in LAN. \nDettagli: " + e);
                    dispatcher.Invoke(() =>
                    {
                        lock_disp.EnterWriteLock();
                        try
                        {
                            Shutdown();
                        }
                        finally
                        {
                            lock_disp.ExitWriteLock();
                        }
                    }, DispatcherPriority.DataBind);
                }
                if(user.IPAddress.CompareTo(myIP) != 0 && user.IPAddress.CompareTo("127.0.0.1")!=0) //se non sono io; 127.0.0.1 indirizzo di loopback
                {
                            bool UserPresente = false;
                            bool no_update = true;
                            User rem_user = null;
                            foreach (User u in UserList)
                            {

                                if (u.IPAddress == user.IPAddress ) //Controllo se è già presente nella lista utenti
                                {
                                    UserPresente = true;
                                    rem_user = u;
                                    no_update = CompareBitmapsLazy(GetBitmap(rem_user.Image), GetBitmap(user.Image)); //controllo se ha cambiato immagine profilo, caso in cui deve essere aggiornat nell'interfaccia
                                    break;
                                }
                            }

                        //se l'utente già c'era e non ci sono state modifiche, aggiorno nella mia lista solo il tempo(aggiungo user con time aggiornato)
                        if (UserPresente == true && OffUser == '1' && no_update==true)
                        {
                                UserList.Remove(rem_user);
                                UserList.Add(user);
                        }

                        if (UserPresente == true && OffUser=='0')
                        {
                                UserList.Remove(rem_user);
                                dispatcher.Invoke(() =>   // serve affinchè l'interfaccia grafica venga gestita dal thread principale in maniera sincrona, bloccante
                                {
                                    lock_disp.EnterWriteLock();
                                    try
                                    {
                                        User removeU = ListaUtenti.ListofUser.Where(x => x.IPAddress == rem_user.IPAddress.ToString()).FirstOrDefault();
                                        ListaUtenti.ListofUser.Remove(removeU);
                                    }
                                    finally
                                    {
                                        lock_disp.ExitWriteLock();
                                    }
                                });
                        }

                        if(no_update==false && UserPresente == true)
                        {
                                UserList.Remove(rem_user);
                                UserList.Add(user);
                            dispatcher.Invoke(() =>   // serve affinchè l'interfaccia grafica venga gestita dal thread principale
                            {
                            lock_disp.EnterWriteLock();
                            try
                            {           //aggiorno tutte le finestre lista utenti aperte, con la rimozione dello user offline
                                        User removeU = ListaUtenti.ListofUser.Where(x => x.IPAddress == rem_user.IPAddress.ToString()).FirstOrDefault();
                                        ListaUtenti.ListofUser.Remove(removeU);
                                        ListaUtenti.ListofUser.Insert(ListaUtenti.ListofUser.Count,user);
                                    }
                                    finally
                                    {
                                        lock_disp.ExitWriteLock();
                                    }
                                });
                        }

                        if (UserPresente == false && OffUser=='1') //se lo user non era già presente nella lista lo aggiungo
                            {
                                UserList.Add(user);

                                dispatcher.Invoke(() =>   // serve affinchè l'interfaccia grafica venga gestita dal thread principale
                                    {
                                             lock_disp.EnterWriteLock();
                                          try
                                          {
                                            ListaUtenti.ListofUser.Insert(ListaUtenti.ListofUser.Count,user);
                                          }
                                        finally
                                          {
                                             lock_disp.ExitWriteLock();
                                          }
                                    });
                                }
                }



                /////elimino l'utente se non ricevo da lui pacchetti per 15 secondi, serve sia se si disabilita la scheda di rete sia se non riceve il pacchetto di ESCI
                List<User> tmp = new List<User>();
                foreach (User u in UserList)
                {
                    if (u.IsConnected() == false) //Isconnected(): funzione che controlla se l'ultimo pacchetto è arrivato meno di 15 sec fa
                    {
                        tmp.Add(u);

                        dispatcher.Invoke(() =>   // serve affinchè l'interfaccia grafica venga gestita dal thread principale
                        {
                            lock_disp.EnterWriteLock();
                            try
                            {   //LIstofUser è una variabile statica della classe ListaUtenti, affinché possa essere condivisa da tutti gli oggetti ListaUtenti 
                                User removeU = ListaUtenti.ListofUser.Where(x => x.IPAddress == u.IPAddress.ToString()).FirstOrDefault();
                                ListaUtenti.ListofUser.Remove(removeU);
                            }
                            finally
                            {
                                lock_disp.ExitWriteLock();
                            }
                        });
                    }
                }
                foreach (User tmp_u in tmp)
                    UserList.Remove(tmp_u);
                ///////////////////////////////////////////////////////////////////////////////////////////////////
            }


            server.Close();
        }
        public BitmapSource GetImageUser(String pathImage) //ritorna l'immagine "pathImage" compressa come BitmapSource
        {
            //legge l'immagine corrispondente a "pathImage" -> salva come Image -> salva come Bitmap -> Salva come BitmapSource
            var bytes = File.ReadAllBytes(pathImage);
            var ms = new MemoryStream(bytes);
            var image1 = Image.FromStream(ms);
            Bitmap bitmapPicture = new Bitmap(image1, 250, 250);
            bitmapSourcePicture = BitmapToBitmapSource(bitmapPicture);

            //procedura per comprimere l'immagine a partire da una Bitmap e salvarla in locale
            ImageCodecInfo jpgEncoder = GetEncoderInfo(ImageFormat.Jpeg);
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameters myEncParams = new EncoderParameters(1);
            EncoderParameter tmp = new EncoderParameter(myEncoder, 20L);
            myEncParams.Param[0] = tmp;
            bitmapPicture.Save(@"./compressedPicture.jpg", jpgEncoder, myEncParams);
            bitmapPicture.Dispose();

            //recupero l'immagine compressa come Bitmap 
            Bitmap source = (Bitmap)MyImageFromFile(@"./compressedPicture.jpg");

            //ritorno una BitmapSource perchè mi serve salvarla nell'oggetto User
            return BitmapToBitmapSource(source);
        }
        private void SendPath(string Path)
        {

            NamedPipeClientStream pipe = new NamedPipeClientStream(".", "pipesendpath", PipeDirection.Out, PipeOptions.None);

            try
            {
                pipe.Connect();
                StreamWriter writer = new StreamWriter(pipe);

                writer.WriteLine(Path);
                writer.Flush();
            }
            catch (Exception e)
            {

                Console.WriteLine("Errore nella trasmissione path del file/cartella da inviare : " + e);
            }

            pipe.Close();
        }
        public void ReceivePath() //wrapper del receive path del path manager in cui il processo riceve il path attraverso la pipe
        {
            while (!cts.Token.IsCancellationRequested)
            {
               
                string path= PathManager.ReceivePath();
                dispatcher.BeginInvoke((Action)delegate //uso begin invoke per non bloccare il thread principale mentro aggiorno l'interfaccia grafica
               
                {
                    lock_disp.EnterWriteLock();

                    try
                    {   //creo la finestra di selezione degli utenti
                        ListaUtentiWLiindow = new ListaUtenti();
                        ListaUtentiWLiindow.ParentWindow = this;
                        ListaUtentiWLiindow.Path = path;
                        ListaUtentiWLiindow.Title = "Condividi " + path;
                        ListaUtentiWLiindow.Show();

                        
                    }

                    catch(Exception e)
                    {
                        System.Windows.MessageBox.Show("Errore Lista utenti.\n Dettagli: " + e);
                    }
                    finally
                    {
                        lock_disp.ExitWriteLock();
                    }
                }, DispatcherPriority.DataBind);
               
            }
                
        }

        public void ReceiveData() //per l'invio dei dati usiamo il protocollo tcp poichè coinvolge solo due host la volta (quindi può essere connection-oriented) ed è più affidabile
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 17000);
            Console.WriteLine("Listening...");
            try
            {
                listener.Start();
            }
            catch (Exception)
            {

                Window1.ShowMessageBox("App già aperta!Chiudere per riaprirla.");

            }
            while (!cts.Token.IsCancellationRequested)
            {    
                TcpClient client = listener.AcceptTcpClient();

                Dispatcher.BeginInvoke((Action)delegate
                {
                    lock_disp.EnterWriteLock();

                    try
                    {
                        Transfer tr = new Transfer(this, client);
                    }
                    finally
                    {
                        lock_disp.ExitWriteLock();
                    }
                }, DispatcherPriority.DataBind);
               

            }
            listener.Stop();
        }







        /*FUNZIONI UTILI*/
        public static Image MyImageFromFile(string path) // dal path del file in locale ritorna un IMAGE
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            return img;
        }
        private ImageCodecInfo GetEncoderInfo(ImageFormat format) //recupera i parametri necessari per la compressione dell'immagine
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        public static bool CompareBitmapsLazy(Bitmap bmp1, Bitmap bmp2) //confronta due bitmap
        {
            if (bmp1 == null || bmp2 == null)
                return false;
            if (object.Equals(bmp1, bmp2)) //controllo se bmp1 e bmp2 sono referenze dello stesso oggetto
                return true;
            if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat))
                return false;

            //Compare bitmaps using GetPixel method
            for (int column = 0; column < bmp1.Width; column++)
            {
                for (int row = 0; row < bmp1.Height; row++)
                {
                    if (!bmp1.GetPixel(column, row).Equals(bmp2.GetPixel(column, row))) //anche se bmp1 e bmp2 non puntano allo stesso oggetto potrebbero comunque riferirsi alla stessa immagine, check pixel per pixel  
                        return false;
                }
            }

            return true;
        }
        public static Bitmap GetBitmap(BitmapSource source) //conversione da bitmap source a bitmap 
        {
            Bitmap bmp = new Bitmap(source.PixelWidth, source.PixelHeight, PixelFormat.Format32bppPArgb);
            BitmapData data = bmp.LockBits(new Rectangle(System.Drawing.Point.Empty, bmp.Size),
                                           ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            source.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }
        public static string BitmapToBase64(Bitmap image)
        {
            using (MemoryStream m = new MemoryStream())
            {
                image.Save(m, ImageFormat.Jpeg);
                byte[] imageBytes = m.ToArray();

                // Convert byte[] to Base64 String
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
        }
        public static BitmapSource BitmapToBitmapSource(Bitmap source)
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                          source.GetHbitmap(),
                          IntPtr.Zero,
                          Int32Rect.Empty,
                          BitmapSizeOptions.FromEmptyOptions());
        }
        public static Bitmap Base64ToBitmap(string ImageText)
        {
            Byte[] bitmapData = Convert.FromBase64String(FixBase64ForImage(ImageText));
            System.IO.MemoryStream streamBitmap = new System.IO.MemoryStream(bitmapData);
            Bitmap bitImage = new Bitmap((Bitmap)Image.FromStream(streamBitmap));
            return bitImage;
        }
        public static string FixBase64ForImage(string Image)
        {
            System.Text.StringBuilder sbText = new System.Text.StringBuilder(Image, Image.Length);
            sbText.Replace("\r\n", String.Empty); sbText.Replace(" ", String.Empty);
            return sbText.ToString();
        }
    }
}
