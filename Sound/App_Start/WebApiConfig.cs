using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Mvc;

namespace Sound
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Конфигурация и службы веб-API

            // Маршруты веб-API
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "SoundApi",
                routeTemplate: "api/{controller}/{action}/{locationId}/{trackId}",
                defaults: new
                {
                    controller = "sound",
                    action = "play",
                    locationId = UrlParameter.Optional,
                    trackId = UrlParameter.Optional
                }
            );
        }
    }
}
