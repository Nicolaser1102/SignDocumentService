namespace SignDocumentService.Services;
using System.ServiceProcess;

    public class WorkerService
    {
        private readonly string _serviceName;

        public WorkerService(string serviceName)
        {
            _serviceName = serviceName;
        }

    public void IniciarServicio()
    {
        using var sc = new ServiceController(_serviceName);

        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Console.WriteLine("llegue hasta aqui");
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        }
    }

        public void DetenerServicio()
        {
            using var sc = new ServiceController(_serviceName);

            if (sc.Status == ServiceControllerStatus.Running)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }

        public ServiceControllerStatus ObtenerEstado()
        {
            using var sc = new ServiceController(_serviceName);
            return sc.Status;
        }
    }
