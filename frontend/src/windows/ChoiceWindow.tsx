// A choice the user answers: the page's dialog, with its options as buttons. Its answer goes to whoever asked.

import { call } from '../bridge';
import { Button } from '../components/Button';

export function ChoiceWindow({ id, title, message, options }: { id: number; title: string; message: string; options: string[] }) {
    return (
        <section className="view flex h-full flex-col gap-2.5 overflow-auto p-3.5">
            <h2 className="m-0 text-sm">{title}</h2>
            <p>{message}</p>
            <div className="buttons mt-auto flex justify-end gap-2 pt-1.5">
                {options.map((option, i) => (
                    <Button
                        key={option}
                        onClick={() => {
                            call('answer', id, i);
                            call('hide');
                        }}
                    >
                        {option}
                    </Button>
                ))}
            </div>
        </section>
    );
}
