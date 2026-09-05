"""Shared LangGraph wiring for the four independently owned LifeLink agents."""

from langgraph.graph import END, StateGraph

from dispatch_agent import run_dispatch
from domain_analysis_agent import run_domain_analysis
from validation_agent import run_validation

from .models import GraphState, WorkflowRequest, WorkflowResponse


def build_graph():
    graph = StateGraph(GraphState)
    graph.add_node("domain_analysis", run_domain_analysis)
    graph.add_node("dispatch", run_dispatch)
    graph.add_node("validation", run_validation)
    graph.set_entry_point("domain_analysis")
    graph.add_edge("domain_analysis", "dispatch")
    graph.add_edge("dispatch", "validation")
    graph.add_edge("validation", END)
    return graph.compile()


WORKFLOW_GRAPH = build_graph()


def run_workflow(request: WorkflowRequest) -> WorkflowResponse:
    state = WORKFLOW_GRAPH.invoke({"request": request, "steps": []})
    return WorkflowResponse(
        workflow_id=request.workflow_id,
        plan=state.get("plan", {}),
        steps=state["steps"],
        outcome=state["outcome"],
        requires_approval=state["outcome"] == "PendingApproval",
        validation=state["validation"],
        proposed_reservation_units=state.get("proposed_reservation_units", 0),
        proposed_recipient_user_ids=state.get("proposed_recipient_user_ids", []),
    )
