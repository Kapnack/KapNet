using System;
using System.Collections.Generic;

namespace ImageCampus.ToolBox.Services
{
    public sealed class ServiceProvider
    {
        private static ServiceProvider instance;
        public static ServiceProvider Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ServiceProvider();
                }
                return instance;
            }
            private set => instance = value;
        }

        private readonly Dictionary<string, Type> dataServiceTypeByReference = new Dictionary<string, Type>();
        private readonly Dictionary<Type, IService> services = new Dictionary<Type, IService>();

        private ServiceProvider() { }

        public void AddService<ServiceType>(IService service) where ServiceType : class, IService
        {
            if (!services.ContainsKey(typeof(ServiceType)))
            { 
                services.Add(typeof(ServiceType), service);
				if (typeof(IDataService).IsAssignableFrom(typeof(ServiceType)))
                    dataServiceTypeByReference.Add((service as IDataService).ServiceReference, typeof(ServiceType));
            }
        }

        public bool RemoveService<ServiceType>() where ServiceType : class, IService
        {
            if (!services.ContainsKey(typeof(ServiceType)))
                throw new KeyNotFoundException();
            return services.Remove(typeof(ServiceType));
        }

        public bool ContainsService<ServiceType>() where ServiceType : class, IService
        {
            return services.ContainsKey(typeof(ServiceType));
        }

        public ServiceType GetService<ServiceType>() where ServiceType : class, IService
        {
            return services[typeof(ServiceType)] as ServiceType;
        }

        public IDataService GetDataService(string serviceReference) 
        {
			if (!dataServiceTypeByReference.ContainsKey(serviceReference))
                return null;
            return services[dataServiceTypeByReference[serviceReference]] as IDataService;
        }

        public void ClearAllServices()
        {
            services.Clear();
            dataServiceTypeByReference.Clear();
        }

        public void ClearAllNonPersistanceServices() 
        {
            List<Type> nonPersistanceServiceTypes = new List<Type>();
            foreach (KeyValuePair<Type, IService> service in services)
            {
                if (!service.Value.IsPersistance)
                    nonPersistanceServiceTypes.Add(service.Key);
            }
            foreach (Type keyToRemove in nonPersistanceServiceTypes)
            {
				if (typeof(IDataService).IsAssignableFrom(keyToRemove))
                    dataServiceTypeByReference.Remove((services[keyToRemove] as IDataService).ServiceReference);
                services.Remove(keyToRemove);
            }
        }
    }
}
