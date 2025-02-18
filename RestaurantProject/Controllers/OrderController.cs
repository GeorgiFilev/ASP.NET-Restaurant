using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RestaurantProject.Data;
using RestaurantProject.Models;

namespace RestaurantProject.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private Repository<Product> _products;
        private Repository<Order> _orders;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context,UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _products = new Repository<Product>(context);
            _orders = new Repository<Order>(context);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            //ViewBag.Products = await _products.GetAllAsync();
            //Retrieve or create an OrderViewModel from session or other state management
            var model = HttpContext.Session.Get<OrderViewModel>("OrderViewModel") ?? new OrderViewModel
            { 
                OrderItems = new List<OrderItemViewModel>(),
                Products = await _products.GetAllAsync()

            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddItem(int prodId,int prodQty)
        {
            var product = await _context.Products.FindAsync(prodId);
            if(product == null)
            {
                return NotFound();
            }
            // Check if the quantity to add is greater than the available stock
            if (prodQty > product.Stock)
            {
                // You can return a custom error message if stock is insufficient
                TempData["ErrorMessage"] = "Insufficient stock available.";
                return RedirectToAction("Index"); // Redirect to an appropriate page
            }

            // Decrease the product's stock by the quantity added
            product.Stock -= prodQty;

            // Save changes to the database to update stock quantity
            _context.Update(product);
            await _context.SaveChangesAsync();

            //retrieve or create OrderViewModel
            var model = HttpContext.Session.Get<OrderViewModel>("OrderViewModel") ?? new OrderViewModel
            {
                OrderItems = new List<OrderItemViewModel>(),
                Products = await _products.GetAllAsync()
            };

            //check if the product is already in the order
            var existingItem = model.OrderItems.FirstOrDefault(oi => oi.ProductId == prodId);

            //if the order is in -> update quantity
            if(existingItem != null)
            {
                existingItem.Quantity += prodQty;
            }
            else
            {
                model.OrderItems.Add(new OrderItemViewModel
                {
                    ProductId = product.ProductId,
                    Price = product.Price,
                    Quantity = prodQty,
                    ProductName = product.Name
                });
            }


            //Update the total ammount
            model.TotalAmount = model.OrderItems.Sum(oi => oi.Price * oi.Quantity);

            //Save updated OrderViewModel to session
            HttpContext.Session.Set("OrderViewModel", model);

            //Redirect back to show updated order items
            return RedirectToAction("Create", model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Cart()
        {
            //Retrieve the OrderView model from session or other state management
            var model = HttpContext.Session.Get<OrderViewModel>("OrderViewModel");
            if(model == null || model.OrderItems.Count == 0)
            {
                return RedirectToAction("Create");
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PlaceOrder()
        {
            var model = HttpContext.Session.Get<OrderViewModel>("OrderViewModel");
            if(model == null || model.OrderItems.Count == 0)
            {
                return RedirectToAction("Create");
            }

            //Create a new Order entity
            Order order = new Order
            {
                OrderDate = DateTime.Now,
                TotalAmount = model.TotalAmount,
                UserId = _userManager.GetUserId(User)
            };

            //Add OrderItems to the Order entity
            foreach (var item in model.OrderItems)
            {
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }
            //save the order
            await _orders.AddAsync(order);

            //clear the orderviewmodel from session\
            HttpContext.Session.Remove("OrderViewModel");

            //Redirect to the Order Confirmation
            return RedirectToAction("ViewOrders");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ViewOrders()
        {
            var userId = _userManager.GetUserId(User);
            var userOrders = await _orders.GetAllByIdAsync(userId, "UserId", new QueryOptions<Order>
            {
                // this basically does a join in order to get the data from the colomn "userId" and apply that we want all the products in the Order
                Includes = "OrderItems.Product"
            });

            return View(userOrders);
        }
    }
}
