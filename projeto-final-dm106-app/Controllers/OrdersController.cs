using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using projeto_final_dm106_app.Models;
using System.Collections;
using projeto_final_dm106_app.br.com.correios.ws;
using projeto_final_dm106_app.CRMClient;
using System.Security.Principal;
using System.Net.Http;
using System.Globalization;

namespace projeto_final_dm106_app.Controllers
{
    [Authorize]
    [RoutePrefix("api/Orders")]
    public class OrdersController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private CalcPrecoPrazoWS correios = new CalcPrecoPrazoWS();
        private CRMRestClient crmClient = new CRMRestClient();
        private string cep_origem = "37540-000";

        enum Status
        {
            NOVO,
            FECHADO,
            CANCELADO,
            ENTREGUE
        };

        // services -------------------------------------------------------------------------------

        // GET: api/Orders
        [Authorize(Roles = "ADMIN")]
        public IList GetOrders()
        {
            return db.Orders.Include(order => order.OrderItems).ToList();
        }

        // GET: api/Orders/email
        public IList GetOrders(string email)
        {
            if (User.IsInRole("ADMIN") || (User.Identity.Name.Equals(email)))
            {
                return db.Orders.Where(order => order.userEmail == email)
                    .Include(order => order.OrderItems).ToList();
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                    System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }
        }

        // GET: api/Orders/5
        [ResponseType(typeof(Order))]
        public IHttpActionResult GetOrder(int id)
        {
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return NotFound();
            }

            if (CheckUserPermission(User, order))
            {
                return Ok(order);
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                   System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }
        }

        // PUT: api/Orders/5
        [Authorize(Roles = "ADMIN")]
        [ResponseType(typeof(void))]
        public IHttpActionResult PutOrder(int id, Order order)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != order.Id)
            {
                return BadRequest("Different Id.");
            }

            if (!OrderExists(id))
            {
                return NotFound();
            }

            db.Entry(order).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Orders
        [ResponseType(typeof(Order))]
        public IHttpActionResult PostOrder(Order order)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (CheckUserPermission(User, order))
            {
                //deafult data
                order.status = Status.NOVO.ToString();
                order.pesoTotal = 0;
                order.precoTotal = 0;
                order.precoFrete = 0;
                order.data = DateTime.Now;
                order.dataEntrega = DateTime.Now;

                db.Orders.Add(order);
                db.SaveChanges();

                return CreatedAtRoute("DefaultApi", new { id = order.Id }, order);
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                     System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }

        }

        // DELETE: api/Orders/5
        [ResponseType(typeof(Order))]
        public IHttpActionResult DeleteOrder(int id)
        {
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return NotFound();
            }

            if (CheckUserPermission(User, order))
            {
                db.Orders.Remove(order);
                db.SaveChanges();

                return Ok(order);
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                   System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }
        }

        // PUT: api/Orders/Close
        [HttpGet]
        [Route("Close")]
        [ResponseType(typeof(void))]
        public IHttpActionResult Close(int id)
        {
            Order order = db.Orders.Find(id);
            if (!OrderExists(id))
            {
                return NotFound();
            }

            if (CheckUserPermission(User, order))
            {
                if (!CalculetedFreight(id))
                {
                    return BadRequest("Freight not calculated.");
                }

                order.status = Status.FECHADO.ToString();

                db.Entry(order).State = EntityState.Modified;

                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return Ok("Order closed with success.");
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                  System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }
        }

        [HttpGet]
        [Route("Freight")]
        [ResponseType(typeof(string))]
        public IHttpActionResult Freight(int id)
        {
            Order order = db.Orders.Find(id);
            if (order == null)
            {
                return NotFound();
            }

            if (CheckUserPermission(User, order))
            {
                // validations --------------------------------------------------------------------
                if (!order.status.Equals(Status.NOVO.ToString()))
                {
                    return BadRequest("Invalid status. Status other than NOVO.");
                }

                if (order.OrderItems.Count == 0)
                {
                    return BadRequest("Empty list items.");
                }

                // cep ----------------------------------------------------------------------------
                Customer customer = crmClient.GetCustomerByEmail(order.userEmail);
                if (customer == null)
                {
                    return BadRequest("Fail when access CRM.");
                }

                string cep_destino = customer.zip;

                // values -------------------------------------------------------------------------
                decimal precoTotal = 0;
                decimal pesoTotal = 0;
                decimal comprimentoTotal = 0;
                decimal alturaTotal = 0;
                decimal larguraTotal = 0;
                decimal diametroTotal = 0;

                foreach (OrderItem orderItem in order.OrderItems)
                {
                    precoTotal += (orderItem.Product.preco * orderItem.quantidade);
                    pesoTotal += (orderItem.Product.peso * orderItem.quantidade);
                    comprimentoTotal += (orderItem.Product.comprimento * orderItem.quantidade);
                    alturaTotal += (orderItem.Product.altura * orderItem.quantidade);
                    larguraTotal += (orderItem.Product.largura * orderItem.quantidade);
                    diametroTotal += (orderItem.Product.diametro * orderItem.quantidade);
                }

                // freight --------------------------------------------------------------------------
                cResultado resultado = correios.CalcPrecoPrazo("", "", "40010", cep_origem,
                    cep_destino, pesoTotal.ToString(), 1, comprimentoTotal, alturaTotal,
                    larguraTotal, diametroTotal, "N", precoTotal, "S");

                if (resultado.Servicos[0].Erro.Equals("0"))
                {
                    order.precoTotal = precoTotal;
                    order.pesoTotal = pesoTotal;
                    order.precoFrete = Decimal.Parse(resultado.Servicos[0].Valor,
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                        new CultureInfo("pt-BR"));
                    order.dataEntrega = DateTime.Now.AddDays(double.Parse(resultado.Servicos[0].PrazoEntrega));

                    db.Entry(order).State = EntityState.Modified;
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        throw;
                    }
                }
                else
                {
                    return BadRequest("Fail when access Correios. Erro: " + resultado.Servicos[0].Erro + "-" + resultado.Servicos[0].MsgErro);
                }

                return Ok("Freight success calculeted.");
            }
            else
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"Message\": \"Authorization has been denied for this request.\"}",
                 System.Text.Encoding.UTF8, "application/json"),
                    ReasonPhrase = "Unauthorized"
                });
            }
        }

        // local ----------------------------------------------------------------------------------
        private bool CheckUserPermission(IPrincipal user, Order order)
        {
            return ((user.Identity.Name.Equals(order.userEmail)) || user.IsInRole("ADMIN"));
        }

        private bool OrderExists(int id)
        {
            return db.Orders.Count(e => e.Id == id) > 0;
        }

        private bool CalculetedFreight(int id)
        {
            return db.Orders.Where(e => e.Id == id).Select(e => e.precoFrete).Single() > 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

    }
}