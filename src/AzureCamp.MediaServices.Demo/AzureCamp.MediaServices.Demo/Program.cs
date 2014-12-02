using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCamp.MediaServices.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            string mediaServiceName = ConfigurationManager.AppSettings["MediaServiceName"];
            string mediaServiceKey = ConfigurationManager.AppSettings["MediaServiceKey"];
            string mediaServiceLiveChannelName = ConfigurationManager.AppSettings["MediaServiceLiveChannelName"];

            MediaServicesCredentials credentials =
                new MediaServicesCredentials(mediaServiceName, mediaServiceKey);

            // création du cloud media context
            CloudMediaContext cloudMediaContext
                = new CloudMediaContext(credentials);

            // récupération du channel live par son nom
            IChannel azureCampLiveChannel = cloudMediaContext.Channels
                .Where(c => c.Name == mediaServiceLiveChannelName)
                .FirstOrDefault();

            // on vérifie que le channel existe bien
            if (azureCampLiveChannel == null)
            {
                Console.WriteLine("Le channel live {0} n'a pas été trouvé sur le service de médias {1}", mediaServiceLiveChannelName, mediaServiceName);
                Console.ReadLine();
                return;
            }

            // on vérifie que le channel est en cours d'exécution
            if (azureCampLiveChannel.State != ChannelState.Running)
            {
                Console.WriteLine("Le channel live {0} n'est pas en cours d'exécution, merci de le démarrer.", mediaServiceLiveChannelName);
                Console.ReadLine();
                return;
            }

            IProgram program = CreateProgram(cloudMediaContext, azureCampLiveChannel);
            Console.WriteLine("Appuyez sur <Entrée> pour arrêter le programme et publier l'asset associé en VOD.");
            Console.ReadLine();

            Uri smoothStreamingUri = StopProgramAndPublishAsset(program, cloudMediaContext);

            Console.WriteLine("Vous pouvez re-visionner le programme à l'adresse : {0}", smoothStreamingUri);

            Console.WriteLine("Appuyez sur <Entrée> pour lire le programme...");
            Console.ReadLine();

            Process.Start(string.Format("http://amsplayer.azurewebsites.net/player.html?player=flash&format=smooth&url={0}", smoothStreamingUri));

            Console.ReadLine();
        }

        private static Uri StopProgramAndPublishAsset(IProgram program, CloudMediaContext cloudMediaContext)
        {
            // on récupère l'id de l'asset lié au programme
            string assetId = program.AssetId;

            Console.WriteLine("Arrêt du programme en cours...");

            // on stop le programme et on le supprime
            // NOTE : il ne peut y avoir que 3 programmes démarrés en simultanés par channel live
            program.Stop();

            Console.WriteLine("Le programme est arrêté.");

            program.Delete();

            Console.WriteLine("Le programme est supprimé.");

            // on récupère l'asset avec son id
            IAsset liveAsset = cloudMediaContext.Assets
                .Where(a => a.Id == assetId)
                .FirstOrDefault();

            // création d'un access policy qui autorise la lecture pendant 2 jours
            IAccessPolicy readPolicy = cloudMediaContext.AccessPolicies
                .Create("CatchupPolicy", TimeSpan.FromDays(2), AccessPermissions.Read);

            // on créé le locator sur l'asset
            ILocator locator = cloudMediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, liveAsset, readPolicy);

            // on récupère l'URL de smooth streaming
            return locator.GetSmoothStreamingUri();
        }

        private static IProgram CreateProgram(CloudMediaContext cloudMediaContext, IChannel azureCampLiveChannel)
        {
            // création d'un asset pour enregistrer un programme
            string programName = string.Format("LiveAzureCamp2014");
            IAsset liveAsset = cloudMediaContext.Assets.Create(programName, AssetCreationOptions.None);

            Console.WriteLine("Création du programme...");

            // création du programme live
            IProgram program = azureCampLiveChannel.Programs
                .Create(new ProgramCreationOptions()
                {
                    ArchiveWindowLength = TimeSpan.FromMinutes(10),
                    Description = "Démonstration AMS Live Azure Camp 2014",
                    Name = programName,
                    AssetId = liveAsset.Id
                });

            Console.WriteLine("Le programme {0} a été créé.", programName);
            Console.WriteLine("Démarrage du programme...");

            // démarrage du programme
            program.Start();

            Console.WriteLine("Le programme {0} a démarré.", programName);

            return program;
        }
    }
}
