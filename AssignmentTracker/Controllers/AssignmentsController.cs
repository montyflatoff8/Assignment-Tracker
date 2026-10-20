using AssignmentTracker.Models;
using AssignmentTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentTracker.Controllers;

/// <summary>
/// CRUD endpoints for the "assignments" resource.
///
/// [ApiController] turns on API-friendly behavior: request bodies are read from JSON
/// automatically, and malformed JSON gets an automatic 400 Bad Request.
/// </summary>
[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly AssignmentStore _store;

    // The store is a singleton registered in Program.cs; ASP.NET passes it in here (dependency injection).
    public AssignmentsController(AssignmentStore store)
    {
        _store = store;
    }


    // GET /api/assignments  -> 200 with every assignment (an empty array when there are none)
    [HttpGet]
    public ActionResult<List<Assignment>> GetAll()
    {
        return Ok(_store.GetAll());
    }


    // GET /api/assignments/{id}  -> 200 with one assignment, or 404
    [HttpGet("{id:int}")]
    public ActionResult<Assignment> GetById(int id)
    {
        var assignment = _store.GetById(id);

        if (assignment is null)
        {
            return AssignmentNotFound(id);
        }

        return Ok(assignment);
    }


    // POST /api/assignments  -> 201 Created with the new assignment and a Location header, or 400
    [HttpPost]
    public ActionResult<Assignment> Create(AssignmentInput input)
    {
        if (!IsValid(input))
        {
            return ValidationProblem(ModelState);
        }

        var created = _store.Add(input);

        // Sets the Location header to /api/assignments/{id} of the new assignment
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }


    // PUT /api/assignments/{id}  -> 200 with the updated assignment, 400, or 404
    [HttpPut("{id:int}")]
    public ActionResult<Assignment> Update(int id, AssignmentInput input)
    {
        if (!IsValid(input))
        {
            return ValidationProblem(ModelState);
        }

        var updated = _store.Update(id, input);

        if (updated is null)
        {
            return AssignmentNotFound(id);
        }

        return Ok(updated);
    }


    // DELETE /api/assignments/{id}  -> 204 No Content, or 404
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        if (!_store.Delete(id))
        {
            return AssignmentNotFound(id);
        }

        return NoContent();
    }


    // Runs our validation rules and copies any errors into ModelState,
    // so ValidationProblem(ModelState) returns them as a standard 400 response.
    private bool IsValid(AssignmentInput input)
    {
        foreach (var (field, messages) in input.Validate())
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        return ModelState.IsValid;
    }


    private ObjectResult AssignmentNotFound(int id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Assignment not found",
            detail: $"No assignment exists with id {id}.");
    }
}
