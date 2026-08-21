using FeedMe.Data;
using FeedMe.DTOs;
using FeedMe.Hubs;
using FeedMe.Models;
using FeedMe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedMe.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IOrderService _orderService;

        public OrdersApiController(ApplicationDbContext context, IHubContext<OrderHub> hubContext, IOrderService orderService)
        {
            _context = context;
            _hubContext = hubContext;
            _orderService = orderService;
        }

        
        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request == null || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { success = false, message = "Invalid order request." });
            }

            if (string.IsNullOrWhiteSpace(request.StudentId))
            {
                return BadRequest(new { success = false, message = "StudentId is required to place an order." });
            }

           
            TimeSlot? activeSlot = null;

            if (request.TimeSlotId.HasValue && request.TimeSlotId.Value > 0)
            {
                activeSlot = await _context.TimeSlots
                    .Include(s => s.Orders)
                    .FirstOrDefaultAsync(s => s.Id == request.TimeSlotId.Value);
            }
            else
            {
               
                var now = DateTime.Now;
                activeSlot = await _context.TimeSlots
                    .Include(s => s.Orders)
                    .Where(s => s.IsActive && s.EndTime > now)
                    .OrderBy(s => s.StartTime)
                    .FirstOrDefaultAsync();
            }

            if (activeSlot == null || !activeSlot.IsActive)
            {
                return BadRequest(new { success = false, message = "No active pickup time slot available. Ordering is currently closed." });
            }

           
            var orderItemDtos = request.Items
                .Select(i => new OrderItemDto { MenuItemId = i.MenuItemId, Quantity = i.Quantity })
                .ToList();

            Order order;
            try
            {
                order = await _orderService.CreateOrderAsync(request.StudentId, activeSlot.Id, orderItemDtos);
            }
            catch (Exception ex)
            {
               
                return BadRequest(new { success = false, message = ex.Message });
            }

           
            if (_hubContext != null)
            {
                await _hubContext.Clients.All.SendAsync("NewOrderReceived", new { orderId = order.Id });
            }

            return Ok(new
            {
                success = true,
                message = "Order placed successfully!",
                orderId = order.Id,
                pickupSlot = activeSlot.SlotTime
            });
        }

        
        public class CreateOrderRequest
        {
            public string? StudentId { get; set; }
            public string? StudentLcid { get; set; }
            public string? StudentName { get; set; }
            public string? Semester { get; set; }
            public int? TimeSlotId { get; set; }
            public List<OrderItemRequest> Items { get; set; } = new();
        }

        public class OrderItemRequest
        {
            public int MenuItemId { get; set; }
            public int Quantity { get; set; }
        }
    }
}