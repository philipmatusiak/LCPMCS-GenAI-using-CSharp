using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using CustomerManagementApp.Models;
using CustomerManagementApp.Services;
using CustomerManagementApp.ViewModels;

namespace CustomerManagementApp.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        
        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }
        
        // GET: /Customer/
        public IActionResult Index()
        {
            var customers = _customerService.GetAllCustomers();
            return View(customers);
        }
        
        // GET: /Customer/Details/5
        public IActionResult Details(int id)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                return NotFound();
            }
            
            return View(customer);
        }
        
        // GET: /Customer/Create
        public IActionResult Create()
        {
            return View();
        }
        
        // POST: /Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CustomerViewModel customerViewModel)
        {
            if (ModelState.IsValid)
            {
                _customerService.CreateCustomer(customerViewModel);
                return RedirectToAction(nameof(Index));
            }
            
            return View(customerViewModel);
        }
        
        // GET: /Customer/Edit/5
        public IActionResult Edit(int id)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                return NotFound();
            }
            
            return View(customer);
        }
        
        // POST: /Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, CustomerViewModel customerViewModel)
        {
            if (id != customerViewModel.ID)
            {
                return NotFound();
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    _customerService.UpdateCustomer(customerViewModel);
                }
                catch (Exception)
                {
                    if (!_customerService.CustomerExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            return View(customerViewModel);
        }
        
        // GET: /Customer/Delete/5
        public IActionResult Delete(int id)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                return NotFound();
            }
            
            return View(customer);
        }
        
        // POST: /Customer/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _customerService.DeleteCustomer(id);
            return RedirectToAction(nameof(Index));
        }
        
        // GET: /Customer/Search
        public IActionResult Search(string searchTerm, bool onlyActive = false)
        {
            var customers = _customerService.SearchCustomers(searchTerm, onlyActive);
            return View("Index", customers);
        }
    }
}
