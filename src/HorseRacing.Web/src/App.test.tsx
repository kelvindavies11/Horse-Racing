import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App from "./App";

describe("racing dashboard", () => {
  it("renders the race-day summary from the repository", async () => {
    render(<App />);

    expect(await screen.findByRole("heading", { name: "Today’s meetings" })).toBeInTheDocument();
    expect(screen.getByText("Demo data")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Ascot" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Race schedule" })).toBeInTheDocument();
  });

  it("filters meetings by code without fetching in a component", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Today’s meetings" });

    await user.click(screen.getByRole("button", { name: "Jump" }));

    const cards = screen.getAllByTestId("meeting-card");
    expect(cards).toHaveLength(1);
    expect(within(cards[0]).getByRole("heading", { name: "Stratford" })).toBeInTheDocument();
  });

  it("supports course search and an empty-state reset", async () => {
    const user = userEvent.setup();
    render(<App />);
    await screen.findByRole("heading", { name: "Today’s meetings" });

    await user.type(screen.getByPlaceholderText("Search racecourse"), "not a real course");
    expect(screen.getByRole("heading", { name: "No meetings match those filters" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(screen.getAllByTestId("meeting-card")).toHaveLength(4);
  });
});

