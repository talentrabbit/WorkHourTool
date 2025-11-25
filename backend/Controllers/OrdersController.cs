using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DbModel;
using System;
using System.Linq;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OrdersController(AppDbContext db)
        {
            _db = db;
        }

        // GET: api/Orders
        [HttpGet]
        public IActionResult GetAll()
        {
            var list = _db.Orders
                .AsNoTracking()
                .Include(o => o.Product)
                .OrderBy(o => o.SerialNo)
                .Select(o => new
                {
                    o.Id,
                    o.SerialNo,
                    o.Customer,
                    o.OrderNumber,
                    o.ProvinceCity,
                    o.Address,
                    o.DeliveryDate,
                    ProductLine = o.Product != null ? o.Product.ProductLine : null,
                    SystemType = o.Product != null ? o.Product.SystemType : null
                })
                .ToList();
            return Ok(list);
        }

        // GET: api/Orders/by-serial/{serialNo}
        [HttpGet("by-serial/{serialNo}")]
        public IActionResult GetBySerial(string serialNo)
        {
            var item = _db.Orders
                .AsNoTracking()
                .Include(o => o.Product)
                .FirstOrDefault(o => o.SerialNo == serialNo);
            if (item == null) return NotFound(new { message = $"Order not found for SerialNo {serialNo}" });
            return Ok(new
            {
                item.Id,
                item.SerialNo,
                item.Customer,
                item.OrderNumber,
                item.ProvinceCity,
                item.Address,
                item.DeliveryDate,
                ProductLine = item.Product?.ProductLine,
                SystemType = item.Product?.SystemType
            });
        }

        public class CreateOrderRequest
        {
            public string SerialNo { get; set; } = string.Empty;
            public string? Customer { get; set; }
            public string? OrderNumber { get; set; }
            public string? ProvinceCity { get; set; }
            public string? Address { get; set; }
            public string? DeliveryDate { get; set; } // stored as TEXT
        }

        public class UpdateOrderRequest
        {
            public string? SerialNo { get; set; }
            public string? Customer { get; set; }
            public string? OrderNumber { get; set; }
            public string? ProvinceCity { get; set; }
            public string? Address { get; set; }
            public string? DeliveryDate { get; set; }
        }

        // POST: api/Orders
        [HttpPost]
        public IActionResult Create([FromBody] CreateOrderRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.SerialNo))
                return BadRequest(new { message = "SerialNo is required" });

            var serial = req.SerialNo.Trim();

            var product = _db.Products.FirstOrDefault(p => p.SerialNo == serial);
            if (product == null)
                return NotFound(new { message = $"Product not found for SerialNo {serial}" });

            // ensure one order per product (SerialNo unique in Orders)
            if (_db.Orders.Any(o => o.SerialNo == serial))
                return Conflict(new { message = $"Order already exists for SerialNo {serial}" });

            // ensure OrderNumber uniqueness if provided
            if (!string.IsNullOrWhiteSpace(req.OrderNumber) && _db.Orders.Any(o => o.OrderNumber == req.OrderNumber))
                return Conflict(new { message = $"OrderNumber '{req.OrderNumber}' already exists" });

            var o = new Order
            {
                SerialNo = serial,
                Customer = req.Customer,
                OrderNumber = req.OrderNumber,
                ProvinceCity = req.ProvinceCity,
                Address = req.Address,
                DeliveryDate = req.DeliveryDate
            };
            _db.Orders.Add(o);
            _db.SaveChanges();

            return Ok(new { message = "Order created", id = o.Id });
        }

        // PUT: api/Orders/{id}
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UpdateOrderRequest req)
        {
            var o = _db.Orders.FirstOrDefault(x => x.Id == id);
            if (o == null) return NotFound(new { message = $"Order {id} not found" });

            if (!string.IsNullOrWhiteSpace(req.SerialNo))
            {
                var newSerial = req.SerialNo.Trim();
                var product = _db.Products.FirstOrDefault(p => p.SerialNo == newSerial);
                if (product == null)
                    return NotFound(new { message = $"Product not found for SerialNo {newSerial}" });
                // avoid violating one-to-one: another order with same serial exists
                if (_db.Orders.Any(x => x.SerialNo == newSerial && x.Id != id))
                    return Conflict(new { message = $"Another order already exists for SerialNo {newSerial}" });
                o.SerialNo = newSerial;
            }

            if (!string.IsNullOrWhiteSpace(req.OrderNumber))
            {
                if (_db.Orders.Any(x => x.OrderNumber == req.OrderNumber && x.Id != id))
                    return Conflict(new { message = $"OrderNumber '{req.OrderNumber}' already exists" });
                o.OrderNumber = req.OrderNumber;
            }

            if (req.Customer != null) o.Customer = req.Customer;
            if (req.ProvinceCity != null) o.ProvinceCity = req.ProvinceCity;
            if (req.Address != null) o.Address = req.Address;
            if (req.DeliveryDate != null) o.DeliveryDate = req.DeliveryDate;

            _db.SaveChanges();
            return Ok(new { message = "Order updated", id = o.Id });
        }

        // DELETE: api/Orders/{id}
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var o = _db.Orders.FirstOrDefault(x => x.Id == id);
            if (o == null) return NotFound(new { message = $"Order {id} not found" });
            _db.Orders.Remove(o);
            _db.SaveChanges();
            return Ok(new { message = "Order deleted", id });
        }
    }
}
