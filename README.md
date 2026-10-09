# AzureServiceBusDemo

Demo para entender **Azure Service Bus** con topics, subscriptions y filtros por `Subject`.

## Esquema

```
  topic order-events      ─ sub: inventory      (Subject = OrderPlaced)
                          ─ sub: notifications  (Subject = OrderConfirmed | OrderRejected)
  topic inventory-events  ─ sub: ordering       (Subject = StockReserved | StockRejected)
```

## Flujo

```
 POST /api/orders
       │
       ▼
 Ordering.Api ──OrderPlaced──► [order-events] ──sub inventory──► Inventory.Worker
                                                                       │ revisa stock
                                                                       ▼
 Ordering.Api ◄──sub ordering── [inventory-events] ◄── StockReserved / StockRejected
       │ actualiza el pedido
       ▼
 Ordering.Api ──OrderConfirmed / OrderRejected──► [order-events] ──sub notifications──► Notifications.Worker
                                                                                         (simula el email)
```

Un mismo topic (`order-events`) lleva **varios tipos de evento**. Cada subscription recibe
solo los que le interesan gracias a su **filtro** sobre la propiedad `Subject` del mensaje.

## Proyectos

| Proyecto | Qué hace |
| --- | --- |
| `Shared.Contracts` | Eventos (records) y nombres de topics, subscriptions y subjects. |
| `Shared.Messaging` | `AddServiceBus()` registra un `ServiceBusClient` singleton y un `IEventPublisher`. |
| `Ordering.Api` | `OrdersController` (publica `OrderPlaced`) + `InventoryEventsConsumer` (escucha `inventory-events`). |
| `Inventory.Worker` | `OrderEventsConsumer`: reserva stock en memoria (producto 1 = 10, 2 = 5, 3 = 0). |
| `Notifications.Worker` | `OrderEventsConsumer`: escribe en el log el "email" al cliente. |

`Catalog.Api` no participa en este esquema.

## Conceptos que muestra el código

- **Un solo `ServiceBusClient` por aplicación** (singleton). Los `ServiceBusSender` se crean una vez por topic y se reutilizan.
- **Publicar**: `ServiceBusMessage` con el evento en JSON, `Subject` (para los filtros), `MessageId` y `ContentType`.
- **Consumir**: `ServiceBusProcessor` dentro de un `BackgroundService`.
  - `AutoCompleteMessages = false` → el mensaje se completa a mano con `CompleteMessageAsync` al final.
  - Si el handler lanza una excepción → el mensaje se *abandona*, vuelve a la subscription y se reintenta
    (`DeliveryCount` sube). Tras 10 intentos (`MaxDeliveryCount`) Service Bus lo mueve a la **dead-letter queue**.
  - Si el mensaje no tiene arreglo (subject inesperado, pedido inexistente) → `DeadLetterMessageAsync` directo, sin reintentos.

## 1. Crear la infraestructura en Azure

Los topics necesitan el tier **Standard** (Basic no soporta topics).

```bash
RG=rg-servicebus-demo
NS=sb-demo-<algo-unico>

az group create -n $RG -l eastus
az servicebus namespace create -g $RG -n $NS --sku Standard

# Topics
az servicebus topic create -g $RG --namespace-name $NS -n order-events
az servicebus topic create -g $RG --namespace-name $NS -n inventory-events

# Subscriptions
az servicebus topic subscription create -g $RG --namespace-name $NS --topic-name order-events     -n inventory
az servicebus topic subscription create -g $RG --namespace-name $NS --topic-name order-events     -n notifications
az servicebus topic subscription create -g $RG --namespace-name $NS --topic-name inventory-events -n ordering
```

### Filtros

Cada subscription nueva trae una regla `$Default` que deja pasar **todo** (`1=1`).
Hay que borrarla y crear reglas con un *correlation filter* sobre `Subject` (en la CLI se llama `--label`).
Si una subscription tiene varias reglas, basta con que **una** coincida (funcionan como OR).

```bash
# order-events / inventory  →  OrderPlaced
az servicebus topic subscription rule delete -g $RG --namespace-name $NS --topic-name order-events --subscription-name inventory -n '$Default'
az servicebus topic subscription rule create -g $RG --namespace-name $NS --topic-name order-events --subscription-name inventory \
  -n OrderPlaced --filter-type CorrelationFilter --label OrderPlaced

# order-events / notifications  →  OrderConfirmed | OrderRejected
az servicebus topic subscription rule delete -g $RG --namespace-name $NS --topic-name order-events --subscription-name notifications -n '$Default'
az servicebus topic subscription rule create -g $RG --namespace-name $NS --topic-name order-events --subscription-name notifications \
  -n OrderConfirmed --filter-type CorrelationFilter --label OrderConfirmed
az servicebus topic subscription rule create -g $RG --namespace-name $NS --topic-name order-events --subscription-name notifications \
  -n OrderRejected --filter-type CorrelationFilter --label OrderRejected

# inventory-events / ordering  →  StockReserved | StockRejected
az servicebus topic subscription rule delete -g $RG --namespace-name $NS --topic-name inventory-events --subscription-name ordering -n '$Default'
az servicebus topic subscription rule create -g $RG --namespace-name $NS --topic-name inventory-events --subscription-name ordering \
  -n StockReserved --filter-type CorrelationFilter --label StockReserved
az servicebus topic subscription rule create -g $RG --namespace-name $NS --topic-name inventory-events --subscription-name ordering \
  -n StockRejected --filter-type CorrelationFilter --label StockRejected
```

> Sin estos filtros, `notifications` recibiría también los `OrderPlaced`: el código los mandaría a la dead-letter queue
> como "UnexpectedSubject". Es una buena forma de ver qué pasa si te olvidas de un filtro.

## 2. Configurar la cadena de conexión

```bash
az servicebus namespace authorization-rule keys list -g $RG --namespace-name $NS -n RootManageSharedAccessKey --query primaryConnectionString -o tsv
```

Se guarda en *user secrets* (no en `appsettings.json`) en los tres proyectos:

```bash
dotnet user-secrets set "ConnectionStrings:ServiceBus" "<cadena>" --project src/Ordering.Api
dotnet user-secrets set "ConnectionStrings:ServiceBus" "<cadena>" --project src/Inventory.Worker
dotnet user-secrets set "ConnectionStrings:ServiceBus" "<cadena>" --project src/Notifications.Worker
```

## 3. Ejecutar

Arranca `Ordering.Api`, `Inventory.Worker` y `Notifications.Worker` (en Visual Studio: *Multiple startup projects*)
y usa [Ordering.Api.http](src/Ordering.Api/Ordering.Api.http) o Swagger (`http://localhost:5180/swagger`):

1. `POST /api/orders` con productos 1 y 2 → responde `202 Accepted` con estado `Pending`.
2. `GET /api/orders/{id}` unos segundos después → `Confirmed`. En la consola de Notifications aparece el "email".
3. `POST /api/orders` con el producto 3 → termina en `Rejected` con el motivo.

En el portal de Azure (namespace → topic → subscription → **Service Bus Explorer**) puedes ver los mensajes
activos y los de la dead-letter queue.

## Fuera del alcance de la demo (para producción)

- **Managed Identity** (`DefaultAzureCredential`) en lugar de cadena de conexión.
- **Idempotencia**: Service Bus entrega *al menos una vez*, un mensaje puede llegar dos veces.
- **Outbox**: aquí se guarda el pedido y luego se publica; si la publicación falla, el pedido queda `Pending`.
- Persistencia real (los pedidos y el stock están en memoria).
